using CoreInventory.Data;
using CoreInventory.Models.Inventory;
using CoreInventory.ViewModels.Dashboard;
using CoreInventory.ViewModels.History;
using CoreInventory.ViewModels.Operations;
using CoreInventory.ViewModels.Settings;
using CoreInventory.ViewModels.Stock;
using Npgsql;

namespace CoreInventory.Services;

public sealed partial class InventoryService
{
    private readonly IPostgresConnectionFactory _connectionFactory;

    public InventoryService(IPostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var model = new DashboardViewModel
        {
            ReceiptOpenCount = await CountAsync(
                connection,
                """
                select count(*)
                from inventory_operation
                where operation_type = @type
                  and status not in ('Done', 'Cancelled');
                """,
                [("type", OperationTypes.Receipt)],
                cancellationToken),
            ReceiptLateCount = await CountAsync(
                connection,
                """
                select count(*)
                from inventory_operation
                where operation_type = @type
                  and schedule_date < current_date
                  and status not in ('Done', 'Cancelled');
                """,
                [("type", OperationTypes.Receipt)],
                cancellationToken),
            DeliveryOpenCount = await CountAsync(
                connection,
                """
                select count(*)
                from inventory_operation
                where operation_type = @type
                  and status not in ('Done', 'Cancelled');
                """,
                [("type", OperationTypes.Delivery)],
                cancellationToken),
            DeliveryWaitingCount = await CountAsync(
                connection,
                """
                select count(*)
                from inventory_operation
                where operation_type = @type
                  and status = 'Waiting';
                """,
                [("type", OperationTypes.Delivery)],
                cancellationToken),
            TotalOperationCount = await CountAsync(
                connection,
                """
                select count(*)
                from inventory_operation
                where status <> 'Cancelled';
                """,
                [],
                cancellationToken),
            LowStockCount = await CountAsync(
                connection,
                """
                select count(*)
                from stock_balance
                where on_hand - allocated <= 5;
                """,
                [],
                cancellationToken)
        };

        model.UpcomingOperations = await QueryOperationListAsync(connection, null, null, 8, cancellationToken);
        model.LowStockItems = await QueryLowStockAsync(connection, 8, cancellationToken);

        return model;
    }

    public async Task<OperationIndexViewModel> GetOperationsAsync(
        string? type,
        string? search,
        string? viewMode,
        CancellationToken cancellationToken = default)
    {
        var selectedType = OperationTypes.Normalize(type);
        var normalizedSearch = search?.Trim();
        var normalizedView = string.Equals(viewMode, "board", StringComparison.OrdinalIgnoreCase) ? "board" : "list";

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var items = await QueryOperationListAsync(connection, selectedType, normalizedSearch, 120, cancellationToken);

        return new OperationIndexViewModel
        {
            SelectedType = selectedType,
            Search = normalizedSearch ?? string.Empty,
            ViewMode = normalizedView,
            OpenCount = items.Count(item => item.Status is not OperationStatuses.Done and not OperationStatuses.Cancelled),
            LateCount = items.Count(item => item.IsLate),
            WaitingCount = items.Count(item => item.IsWaiting),
            Items = items,
            Board = OperationStatuses.Board.ToDictionary(
                status => status,
                status => (IReadOnlyList<OperationListItemViewModel>)items.Where(item => item.Status == status).ToList())
        };
    }

    public async Task<OperationEditorViewModel> GetOperationEditorAsync(
        long? id,
        string? type,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var model = new OperationEditorViewModel
        {
            Type = OperationTypes.Normalize(type),
            ScheduleDate = DateTime.Today.ToString("yyyy-MM-dd"),
            Lines = [new OperationLineInputViewModel { Quantity = 1 }]
        };

        if (id.HasValue)
        {
            var context = await LoadOperationContextAsync(connection, null, id.Value, cancellationToken);
            model.Id = context.Id;
            model.Type = context.Type;
            model.Status = context.Status;
            model.Reference = context.Reference;
            model.WarehouseId = context.WarehouseId;
            model.FromLocationId = context.FromLocationId;
            model.ToLocationId = context.ToLocationId;
            model.ContactName = context.ContactName;
            model.DeliveryAddress = context.DeliveryAddress;
            model.ScheduleDate = context.ScheduleDate;
            model.ResponsibleUserId = context.ResponsibleUserId;
            model.Notes = context.Notes;
            model.Lines = context.Lines
                .Select(line => new OperationLineInputViewModel
                {
                    Id = line.Id,
                    ProductLabel = line.ProductLabel,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    Note = line.Note
                })
                .ToList();
        }

        model.WarehouseOptions = await GetWarehouseOptionsAsync(connection, cancellationToken);
        if (model.WarehouseId == 0 && model.WarehouseOptions.Count > 0)
        {
            model.WarehouseId = model.WarehouseOptions[0].Value;
        }

        model.LocationOptions = await GetLocationOptionsAsync(
            connection,
            null,
            cancellationToken);
        model.ProductOptions = await GetProductOptionsAsync(connection, cancellationToken);
        model.UserOptions = await GetUserOptionsAsync(connection, cancellationToken);
        model.Actions = model.Id.HasValue ? BuildOperationActions(model.Status) : [];

        if (model.Type == OperationTypes.Delivery || model.Type == OperationTypes.Adjustment)
        {
            foreach (var line in model.Lines)
            {
                if (!model.FromLocationId.HasValue)
                {
                    continue;
                }

                line.AvailableStock = await GetAvailableStockAsync(
                    connection,
                    null,
                    model.WarehouseId,
                    model.FromLocationId.Value,
                    line.ProductId,
                    cancellationToken);
                line.InsufficientStock = line.Quantity > line.AvailableStock;
            }
        }

        return model;
    }

    public async Task<long> SaveOperationAsync(
        OperationEditorViewModel model,
        long? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId is null)
        {
            throw new InvalidOperationException("You must be logged in to save an operation.");
        }

        model.Type = OperationTypes.Normalize(model.Type);
        ValidateOperationEditor(model);
        var scheduleDate = ParseDate(model.ScheduleDate);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (model.FromLocationId.HasValue)
        {
            await EnsureLocationWarehouseMatchAsync(connection, transaction, model.WarehouseId, model.FromLocationId.Value, "From location", cancellationToken);
        }

        if (model.ToLocationId.HasValue)
        {
            await EnsureLocationWarehouseMatchAsync(connection, transaction, model.WarehouseId, model.ToLocationId.Value, "To location", cancellationToken);
        }

        long operationId;
        if (!model.Id.HasValue)
        {
            var reference = await GenerateReferenceAsync(connection, transaction, model.WarehouseId, model.Type, cancellationToken);
            operationId = await InsertOperationAsync(connection, transaction, model, currentUserId.Value, reference, scheduleDate, cancellationToken);
        }
        else
        {
            operationId = model.Id.Value;
            var existing = await LoadOperationContextAsync(connection, transaction, operationId, cancellationToken);
            if (existing.Status is OperationStatuses.Done or OperationStatuses.Cancelled)
            {
                throw new InvalidOperationException("Completed or cancelled operations cannot be edited.");
            }

            await UpdateOperationAsync(connection, transaction, model, scheduleDate, cancellationToken);
            await DeleteOperationLinesAsync(connection, transaction, operationId, cancellationToken);
        }

        foreach (var line in model.Lines.Where(line => line.ProductId > 0))
        {
            await InsertOperationLineAsync(connection, transaction, operationId, line, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return operationId;
    }

    public async Task<string> TransitionOperationAsync(
        long operationId,
        string action,
        long? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId is null)
        {
            throw new InvalidOperationException("You must be logged in to update an operation.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await LoadOperationContextAsync(connection, transaction, operationId, cancellationToken);
        var message = await TransitionOperationInternalAsync(
            connection,
            transaction,
            context,
            action,
            currentUserId.Value,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return message;
    }

    public async Task<OperationPrintViewModel> GetOperationPrintAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await BuildPrintModelAsync(connection, id, cancellationToken);
    }

    public async Task<StockIndexViewModel> GetStockIndexAsync(
        string? search,
        long? productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var normalizedSearch = search?.Trim();

        var model = new StockIndexViewModel
        {
            Search = normalizedSearch ?? string.Empty,
            Products = await QueryProductsAsync(connection, cancellationToken),
            Balances = await QueryBalancesAsync(connection, normalizedSearch, cancellationToken),
            ProductOptions = await GetProductOptionsAsync(connection, cancellationToken),
            WarehouseOptions = await GetWarehouseOptionsAsync(connection, cancellationToken)
        };

        model.LocationOptions = await GetLocationOptionsAsync(connection, null, cancellationToken);

        if (productId.HasValue)
        {
            model.ProductForm = await GetProductFormAsync(connection, productId.Value, cancellationToken);
        }

        if (model.ProductOptions.Count > 0)
        {
            model.AdjustmentForm.ProductId = model.ProductOptions[0].Value;
        }

        if (model.WarehouseOptions.Count > 0)
        {
            model.AdjustmentForm.WarehouseId = model.WarehouseOptions[0].Value;
        }

        if (model.LocationOptions.Count > 0)
        {
            model.AdjustmentForm.LocationId = model.LocationOptions[0].Value;
        }

        return model;
    }

    public async Task SaveProductAsync(ProductFormViewModel model, CancellationToken cancellationToken = default)
    {
        ValidateProductForm(model);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await EnsureProductSkuAvailableAsync(connection, model.Id, model.Sku, cancellationToken);

        if (model.Id.HasValue)
        {
            await UpdateProductAsync(connection, model, cancellationToken);
        }
        else
        {
            await InsertProductAsync(connection, model, cancellationToken);
        }
    }

    public async Task AdjustStockAsync(
        StockAdjustmentFormViewModel model,
        long? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId is null)
        {
            throw new InvalidOperationException("You must be logged in to adjust stock.");
        }

        if (model.QuantityDelta == 0)
        {
            throw new InvalidOperationException("Quantity delta must not be zero.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsureLocationWarehouseMatchAsync(connection, transaction, model.WarehouseId, model.LocationId, "Location", cancellationToken);

        await UpsertBalanceAsync(
            connection,
            transaction,
            model.WarehouseId,
            model.LocationId,
            model.ProductId,
            model.QuantityDelta,
            cancellationToken);

        await InsertStockMoveAsync(
            connection,
            transaction,
            reference: $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            warehouseId: model.WarehouseId,
            productId: model.ProductId,
            fromLocationId: model.QuantityDelta < 0 ? model.LocationId : null,
            toLocationId: model.QuantityDelta > 0 ? model.LocationId : null,
            moveKind: model.QuantityDelta >= 0 ? "IN" : "OUT",
            quantity: Math.Abs(model.QuantityDelta),
            userId: currentUserId.Value,
            note: string.IsNullOrWhiteSpace(model.Note) ? "Manual stock adjustment" : model.Note.Trim(),
            operationId: null,
            operationLineId: null,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HistoryIndexViewModel> GetHistoryAsync(string? search, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return new HistoryIndexViewModel
        {
            Search = search?.Trim() ?? string.Empty,
            Moves = await QueryMoveHistoryAsync(connection, search?.Trim(), cancellationToken)
        };
    }

    public async Task<SettingsIndexViewModel> GetSettingsAsync(
        long? warehouseId,
        long? locationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await BuildSettingsModelAsync(connection, warehouseId, locationId, cancellationToken);
    }

    public async Task SaveWarehouseAsync(WarehouseFormViewModel model, CancellationToken cancellationToken = default)
    {
        ValidateWarehouseForm(model);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await EnsureWarehouseCodeAvailableAsync(connection, model.Id, model.Code, cancellationToken);

        if (model.Id.HasValue)
        {
            await UpdateWarehouseAsync(connection, model, cancellationToken);
        }
        else
        {
            await InsertWarehouseAsync(connection, model, cancellationToken);
        }
    }

    public async Task SaveLocationAsync(LocationFormViewModel model, CancellationToken cancellationToken = default)
    {
        ValidateLocationForm(model);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await EnsureLocationCodeAvailableAsync(connection, model.Id, model.WarehouseId, model.Code, cancellationToken);

        if (model.Id.HasValue)
        {
            await UpdateLocationAsync(connection, model, cancellationToken);
        }
        else
        {
            await InsertLocationAsync(connection, model, cancellationToken);
        }
    }
}
