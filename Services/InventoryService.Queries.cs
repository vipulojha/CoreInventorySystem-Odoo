using CoreInventory.Models.Inventory;
using CoreInventory.ViewModels.History;
using CoreInventory.ViewModels.Operations;
using CoreInventory.ViewModels.Settings;
using CoreInventory.ViewModels.Shared;
using CoreInventory.ViewModels.Stock;
using Npgsql;

namespace CoreInventory.Services;

public sealed partial class InventoryService
{
    private async Task<List<OperationListItemViewModel>> QueryOperationListAsync(
        NpgsqlConnection connection,
        string? type,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<OperationListItemViewModel>();

        await using var command = new NpgsqlCommand(
            """
            select
                o.id,
                o.reference,
                o.operation_type,
                o.contact_name,
                to_char(o.schedule_date, 'YYYY-MM-DD') as schedule_date,
                o.status,
                w.code,
                coalesce(fl.code, 'Vendor') as from_location,
                coalesce(tl.code, 'Customer') as to_location,
                count(l.id) as line_count,
                case when o.schedule_date < current_date and o.status not in ('Done', 'Cancelled') then true else false end as is_late
            from inventory_operation o
            join warehouse w on w.id = o.warehouse_id
            left join inventory_location fl on fl.id = o.from_location_id
            left join inventory_location tl on tl.id = o.to_location_id
            left join inventory_operation_line l on l.operation_id = o.id
            where (@type = '' or o.operation_type = @type)
              and (@search = '' or o.reference ilike @likeSearch or o.contact_name ilike @likeSearch)
            group by o.id, w.code, fl.code, tl.code
            order by o.schedule_date asc, o.updated_at desc
            limit @limit;
            """,
            connection);

        command.Parameters.AddWithValue("type", type ?? string.Empty);
        command.Parameters.AddWithValue("search", search ?? string.Empty);
        command.Parameters.AddWithValue("likeSearch", $"%{search ?? string.Empty}%");
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = reader.GetString(5);
            items.Add(new OperationListItemViewModel
            {
                Id = reader.GetInt64(0),
                Reference = reader.GetString(1),
                Type = reader.GetString(2),
                ContactName = reader.GetString(3),
                ScheduleDate = reader.GetString(4),
                Status = status,
                WarehouseCode = reader.GetString(6),
                FromLocationLabel = reader.GetString(7),
                ToLocationLabel = reader.GetString(8),
                LineCount = Convert.ToInt32(reader.GetInt64(9)),
                IsLate = reader.GetBoolean(10),
                IsWaiting = status == OperationStatuses.Waiting
            });
        }

        return items;
    }

    private async Task<List<StockBalanceRowViewModel>> QueryLowStockAsync(
        NpgsqlConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = new List<StockBalanceRowViewModel>();

        await using var command = new NpgsqlCommand(
            """
            select
                p.name,
                p.sku,
                p.unit_cost,
                w.code,
                l.code,
                b.on_hand,
                (b.on_hand - b.allocated) as free_to_use,
                to_char(b.updated_at, 'DD Mon YYYY HH24:MI') as updated_at
            from stock_balance b
            join product p on p.id = b.product_id
            join warehouse w on w.id = b.warehouse_id
            join inventory_location l on l.id = b.location_id
            where b.on_hand - b.allocated <= 5
            order by free_to_use asc, p.name asc
            limit @limit;
            """,
            connection);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockBalanceRowViewModel
            {
                ProductName = reader.GetString(0),
                Sku = reader.GetString(1),
                UnitCost = reader.GetDecimal(2),
                WarehouseCode = reader.GetString(3),
                LocationCode = reader.GetString(4),
                OnHand = reader.GetInt32(5),
                FreeToUse = reader.GetInt32(6),
                UpdatedAt = reader.GetString(7),
                IsLowStock = true
            });
        }

        return rows;
    }

    private async Task<List<ProductCatalogItemViewModel>> QueryProductsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var products = new List<ProductCatalogItemViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select id, sku, name, unit_cost, to_char(updated_at, 'DD Mon YYYY') as updated_at
            from product
            order by name asc;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new ProductCatalogItemViewModel
            {
                Id = reader.GetInt64(0),
                Sku = reader.GetString(1),
                Name = reader.GetString(2),
                UnitCost = reader.GetDecimal(3),
                LastUpdated = reader.GetString(4)
            });
        }

        return products;
    }

    private async Task<List<StockBalanceRowViewModel>> QueryBalancesAsync(
        NpgsqlConnection connection,
        string? search,
        CancellationToken cancellationToken)
    {
        var balances = new List<StockBalanceRowViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select
                p.name,
                p.sku,
                p.unit_cost,
                w.code,
                l.code,
                b.on_hand,
                (b.on_hand - b.allocated) as free_to_use,
                to_char(b.updated_at, 'DD Mon YYYY HH24:MI') as updated_at
            from stock_balance b
            join product p on p.id = b.product_id
            join warehouse w on w.id = b.warehouse_id
            join inventory_location l on l.id = b.location_id
            where (@search = '' or p.name ilike @likeSearch or p.sku ilike @likeSearch or w.code ilike @likeSearch or l.code ilike @likeSearch)
            order by p.name asc, w.code asc, l.code asc;
            """,
            connection);

        command.Parameters.AddWithValue("search", search ?? string.Empty);
        command.Parameters.AddWithValue("likeSearch", $"%{search ?? string.Empty}%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var freeToUse = reader.GetInt32(6);
            balances.Add(new StockBalanceRowViewModel
            {
                ProductName = reader.GetString(0),
                Sku = reader.GetString(1),
                UnitCost = reader.GetDecimal(2),
                WarehouseCode = reader.GetString(3),
                LocationCode = reader.GetString(4),
                OnHand = reader.GetInt32(5),
                FreeToUse = freeToUse,
                UpdatedAt = reader.GetString(7),
                IsLowStock = freeToUse <= 5
            });
        }

        return balances;
    }

    private async Task<ProductFormViewModel> GetProductFormAsync(
        NpgsqlConnection connection,
        long productId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select id, sku, name, unit_cost from product where id = @id;",
            connection);
        command.Parameters.AddWithValue("id", productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Product was not found.");
        }

        return new ProductFormViewModel
        {
            Id = reader.GetInt64(0),
            Sku = reader.GetString(1),
            Name = reader.GetString(2),
            UnitCost = reader.GetDecimal(3)
        };
    }

    private async Task<List<WarehouseRowViewModel>> QueryWarehousesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var warehouses = new List<WarehouseRowViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select id, code, name, address
            from warehouse
            order by code asc;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            warehouses.Add(new WarehouseRowViewModel
            {
                Id = reader.GetInt64(0),
                Code = reader.GetString(1),
                Name = reader.GetString(2),
                Address = reader.GetString(3)
            });
        }

        return warehouses;
    }

    private async Task<List<LocationRowViewModel>> QueryLocationsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var locations = new List<LocationRowViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select l.id, w.code, l.code, l.name, l.kind
            from inventory_location l
            join warehouse w on w.id = l.warehouse_id
            order by w.code asc, l.code asc;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            locations.Add(new LocationRowViewModel
            {
                Id = reader.GetInt64(0),
                WarehouseCode = reader.GetString(1),
                Code = reader.GetString(2),
                Name = reader.GetString(3),
                Kind = reader.GetString(4)
            });
        }

        return locations;
    }

    private async Task<LocationFormViewModel> GetLocationFormAsync(
        NpgsqlConnection connection,
        long locationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select id, warehouse_id, code, name, kind from inventory_location where id = @id;",
            connection);
        command.Parameters.AddWithValue("id", locationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Location was not found.");
        }

        return new LocationFormViewModel
        {
            Id = reader.GetInt64(0),
            WarehouseId = reader.GetInt64(1),
            Code = reader.GetString(2),
            Name = reader.GetString(3),
            Kind = reader.GetString(4)
        };
    }

    private async Task<List<SelectOptionViewModel>> GetWarehouseOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var options = new List<SelectOptionViewModel>();
        await using var command = new NpgsqlCommand(
            "select id, code, name from warehouse order by code asc;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new SelectOptionViewModel
            {
                Value = reader.GetInt64(0),
                Label = $"{reader.GetString(1)} - {reader.GetString(2)}",
                Group = reader.GetString(1)
            });
        }

        return options;
    }

    private async Task<List<SelectOptionViewModel>> GetLocationOptionsAsync(
        NpgsqlConnection connection,
        long? warehouseId,
        CancellationToken cancellationToken)
    {
        var options = new List<SelectOptionViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select l.id, w.code, l.code, l.name
            from inventory_location l
            join warehouse w on w.id = l.warehouse_id
            where (@warehouseId is null or l.warehouse_id = @warehouseId)
            order by w.code asc, l.code asc;
            """,
            connection);

        AddNullableBigint(command, "warehouseId", warehouseId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new SelectOptionViewModel
            {
                Value = reader.GetInt64(0),
                Label = $"{reader.GetString(1)}/{reader.GetString(2)} - {reader.GetString(3)}",
                Group = reader.GetString(1)
            });
        }

        return options;
    }

    private async Task<List<SelectOptionViewModel>> GetProductOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var options = new List<SelectOptionViewModel>();
        await using var command = new NpgsqlCommand(
            "select id, sku, name from product order by name asc;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new SelectOptionViewModel
            {
                Value = reader.GetInt64(0),
                Label = $"[{reader.GetString(1)}] {reader.GetString(2)}"
            });
        }

        return options;
    }

    private async Task<List<SelectOptionViewModel>> GetUserOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var options = new List<SelectOptionViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select id, display_name
            from app_user
            where is_active = true
            order by display_name asc;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new SelectOptionViewModel
            {
                Value = reader.GetInt64(0),
                Label = reader.GetString(1)
            });
        }

        return options;
    }

    private static List<OperationActionViewModel> BuildOperationActions(string status)
    {
        var actions = new List<OperationActionViewModel>();

        if (status is OperationStatuses.Draft or OperationStatuses.Waiting)
        {
            actions.Add(new OperationActionViewModel { Action = "todo", Label = "To Do", Tone = "primary" });
        }

        if (status == OperationStatuses.Ready)
        {
            actions.Add(new OperationActionViewModel { Action = "validate", Label = "Validate", Tone = "success" });
        }

        if (status is not OperationStatuses.Done and not OperationStatuses.Cancelled)
        {
            actions.Add(new OperationActionViewModel { Action = "cancel", Label = "Cancel", Tone = "ghost" });
        }

        return actions;
    }

    private async Task<IReadOnlyList<StockMoveRowViewModel>> QueryMoveHistoryAsync(
        NpgsqlConnection connection,
        string? search,
        CancellationToken cancellationToken)
    {
        var moves = new List<StockMoveRowViewModel>();
        await using var command = new NpgsqlCommand(
            """
            select
                to_char(m.event_at, 'DD Mon YYYY HH24:MI') as event_at,
                m.reference,
                p.name,
                p.sku,
                m.move_kind,
                coalesce(fw.code || '/' || fl.code, '-') as from_location,
                coalesce(tw.code || '/' || tl.code, '-') as to_location,
                m.quantity,
                coalesce(u.display_name, '-') as performed_by,
                m.note
            from stock_move m
            join product p on p.id = m.product_id
            join warehouse w on w.id = m.warehouse_id
            left join inventory_location fl on fl.id = m.from_location_id
            left join warehouse fw on fw.id = fl.warehouse_id
            left join inventory_location tl on tl.id = m.to_location_id
            left join warehouse tw on tw.id = tl.warehouse_id
            left join app_user u on u.id = m.performed_by_user_id
            where (@search = '' or m.reference ilike @likeSearch or p.name ilike @likeSearch or p.sku ilike @likeSearch)
            order by m.event_at desc
            limit 200;
            """,
            connection);

        command.Parameters.AddWithValue("search", search ?? string.Empty);
        command.Parameters.AddWithValue("likeSearch", $"%{search ?? string.Empty}%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            moves.Add(new StockMoveRowViewModel
            {
                EventAt = reader.GetString(0),
                Reference = reader.GetString(1),
                ProductName = reader.GetString(2),
                Sku = reader.GetString(3),
                MoveKind = reader.GetString(4),
                FromLocationLabel = reader.GetString(5),
                ToLocationLabel = reader.GetString(6),
                Quantity = reader.GetInt32(7),
                PerformedBy = reader.GetString(8),
                Note = reader.GetString(9)
            });
        }

        return moves;
    }

    private async Task<SettingsIndexViewModel> BuildSettingsModelAsync(
        NpgsqlConnection connection,
        long? warehouseId,
        long? locationId,
        CancellationToken cancellationToken)
    {
        var warehouses = await QueryWarehousesAsync(connection, cancellationToken);
        var locations = await QueryLocationsAsync(connection, cancellationToken);

        var model = new SettingsIndexViewModel
        {
            Warehouses = warehouses,
            Locations = locations,
            WarehouseOptions = warehouses
                .Select(warehouse => new SelectOptionViewModel
                {
                    Value = warehouse.Id,
                    Label = $"{warehouse.Code} - {warehouse.Name}",
                    Group = warehouse.Code
                })
                .ToList()
        };

        if (warehouseId.HasValue)
        {
            var warehouse = warehouses.FirstOrDefault(item => item.Id == warehouseId.Value)
                            ?? throw new InvalidOperationException("Warehouse was not found.");
            model.WarehouseForm = new WarehouseFormViewModel
            {
                Id = warehouse.Id,
                Code = warehouse.Code,
                Name = warehouse.Name,
                Address = warehouse.Address
            };
        }

        if (locationId.HasValue)
        {
            model.LocationForm = await GetLocationFormAsync(connection, locationId.Value, cancellationToken);
        }
        else if (model.WarehouseOptions.Count > 0)
        {
            model.LocationForm.WarehouseId = model.WarehouseOptions[0].Value;
        }

        return model;
    }

    private async Task<OperationPrintViewModel> BuildPrintModelAsync(
        NpgsqlConnection connection,
        long id,
        CancellationToken cancellationToken)
    {
        var context = await LoadOperationContextAsync(connection, null, id, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            select
                w.name as warehouse_name,
                coalesce(fl.code, '-') as from_location_code,
                coalesce(tl.code, '-') as to_location_code,
                coalesce(u.display_name, '-') as responsible_name
            from inventory_operation o
            join warehouse w on w.id = o.warehouse_id
            left join inventory_location fl on fl.id = o.from_location_id
            left join inventory_location tl on tl.id = o.to_location_id
            left join app_user u on u.id = o.responsible_user_id
            where o.id = @id;
            """,
            connection);

        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Operation was not found.");
        }

        return new OperationPrintViewModel
        {
            Reference = context.Reference,
            Type = context.Type,
            Status = context.Status,
            WarehouseName = reader.GetString(0),
            FromLocationLabel = reader.GetString(1),
            ToLocationLabel = reader.GetString(2),
            Responsible = reader.GetString(3),
            ContactName = context.ContactName,
            DeliveryAddress = context.DeliveryAddress,
            ScheduleDate = context.ScheduleDate,
            Notes = context.Notes,
            Lines = context.Lines
                .Select(line => new OperationLineInputViewModel
                {
                    Id = line.Id,
                    ProductLabel = line.ProductLabel,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    Note = line.Note
                })
                .ToList()
        };
    }

    private static async Task<int> CountAsync(
        NpgsqlConnection connection,
        string sql,
        IEnumerable<(string Name, string Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }
}
