using CoreInventory.Models.Inventory;
using CoreInventory.ViewModels.Operations;
using CoreInventory.ViewModels.Settings;
using CoreInventory.ViewModels.Stock;
using Npgsql;
using NpgsqlTypes;

namespace CoreInventory.Services;

public sealed partial class InventoryService
{
    private async Task<long> InsertOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationEditorViewModel model,
        long currentUserId,
        string reference,
        DateTime scheduleDate,
        CancellationToken cancellationToken)
    {
        await using var insertCommand = new NpgsqlCommand(
            """
            insert into inventory_operation
            (
                reference,
                operation_type,
                status,
                warehouse_id,
                from_location_id,
                to_location_id,
                contact_name,
                delivery_address,
                schedule_date,
                responsible_user_id,
                notes,
                created_by_user_id,
                created_at,
                updated_at
            )
            values
            (
                @reference,
                @type,
                @status,
                @warehouseId,
                @fromLocationId,
                @toLocationId,
                @contactName,
                @deliveryAddress,
                @scheduleDate,
                @responsibleUserId,
                @notes,
                @createdByUserId,
                now(),
                now()
            )
            returning id;
            """,
            connection,
            transaction);

        insertCommand.Parameters.AddWithValue("reference", reference);
        insertCommand.Parameters.AddWithValue("type", model.Type);
        insertCommand.Parameters.AddWithValue("status", OperationStatuses.Draft);
        insertCommand.Parameters.AddWithValue("warehouseId", model.WarehouseId);
        AddNullableBigint(insertCommand, "fromLocationId", model.FromLocationId);
        AddNullableBigint(insertCommand, "toLocationId", model.ToLocationId);
        insertCommand.Parameters.AddWithValue("contactName", model.ContactName.Trim());
        insertCommand.Parameters.AddWithValue("deliveryAddress", model.DeliveryAddress.Trim());
        insertCommand.Parameters.AddWithValue("scheduleDate", scheduleDate);
        AddNullableBigint(insertCommand, "responsibleUserId", model.ResponsibleUserId);
        insertCommand.Parameters.AddWithValue("notes", model.Notes.Trim());
        insertCommand.Parameters.AddWithValue("createdByUserId", currentUserId);

        return Convert.ToInt64(await insertCommand.ExecuteScalarAsync(cancellationToken));
    }

    private async Task UpdateOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationEditorViewModel model,
        DateTime scheduleDate,
        CancellationToken cancellationToken)
    {
        await using var updateCommand = new NpgsqlCommand(
            """
            update inventory_operation
            set warehouse_id = @warehouseId,
                from_location_id = @fromLocationId,
                to_location_id = @toLocationId,
                contact_name = @contactName,
                delivery_address = @deliveryAddress,
                schedule_date = @scheduleDate,
                responsible_user_id = @responsibleUserId,
                notes = @notes,
                updated_at = now()
            where id = @id;
            """,
            connection,
            transaction);

        updateCommand.Parameters.AddWithValue("id", model.Id!.Value);
        updateCommand.Parameters.AddWithValue("warehouseId", model.WarehouseId);
        AddNullableBigint(updateCommand, "fromLocationId", model.FromLocationId);
        AddNullableBigint(updateCommand, "toLocationId", model.ToLocationId);
        updateCommand.Parameters.AddWithValue("contactName", model.ContactName.Trim());
        updateCommand.Parameters.AddWithValue("deliveryAddress", model.DeliveryAddress.Trim());
        updateCommand.Parameters.AddWithValue("scheduleDate", scheduleDate);
        AddNullableBigint(updateCommand, "responsibleUserId", model.ResponsibleUserId);
        updateCommand.Parameters.AddWithValue("notes", model.Notes.Trim());

        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteOperationLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long operationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "delete from inventory_operation_line where operation_id = @operationId;",
            connection,
            transaction);
        command.Parameters.AddWithValue("operationId", operationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOperationLineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long operationId,
        OperationLineInputViewModel line,
        CancellationToken cancellationToken)
    {
        await using var lineCommand = new NpgsqlCommand(
            """
            insert into inventory_operation_line
            (operation_id, product_id, quantity, unit_cost, note)
            values (@operationId, @productId, @quantity, @unitCost, @note);
            """,
            connection,
            transaction);

        lineCommand.Parameters.AddWithValue("operationId", operationId);
        lineCommand.Parameters.AddWithValue("productId", line.ProductId);
        lineCommand.Parameters.AddWithValue("quantity", line.Quantity);
        lineCommand.Parameters.AddWithValue("unitCost", line.UnitCost);
        lineCommand.Parameters.AddWithValue("note", line.Note.Trim());

        await lineCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string> TransitionOperationInternalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationContext context,
        string action,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedAction = action.Trim().ToLowerInvariant();
        if (context.Status == OperationStatuses.Cancelled)
        {
            throw new InvalidOperationException("Cancelled operations cannot be changed.");
        }

        return normalizedAction switch
        {
            "todo" => await MoveOperationToReadyAsync(connection, transaction, context, cancellationToken),
            "validate" => await ValidateOperationAsync(connection, transaction, context, currentUserId, cancellationToken),
            "cancel" => await CancelOperationAsync(connection, transaction, context, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported action.")
        };
    }

    private async Task<string> MoveOperationToReadyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Status == OperationStatuses.Done)
        {
            throw new InvalidOperationException("Completed operations cannot be moved again.");
        }

        if (await HasStockShortageAsync(connection, transaction, context, cancellationToken))
        {
            await UpdateOperationStatusAsync(connection, transaction, context.Id, OperationStatuses.Waiting, cancellationToken);
            return "Stock is short for at least one line. The operation was moved to Waiting.";
        }

        await UpdateOperationStatusAsync(connection, transaction, context.Id, OperationStatuses.Ready, cancellationToken);
        return "Operation moved to Ready.";
    }

    private async Task<string> ValidateOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationContext context,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        if (context.Status != OperationStatuses.Ready)
        {
            throw new InvalidOperationException("Only Ready operations can be validated.");
        }

        await ApplyOperationAsync(connection, transaction, context, currentUserId, cancellationToken);
        await UpdateOperationStatusAsync(connection, transaction, context.Id, OperationStatuses.Done, cancellationToken);
        return "Operation completed and stock movement was posted.";
    }

    private async Task<string> CancelOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Status == OperationStatuses.Done)
        {
            throw new InvalidOperationException("Completed operations cannot be cancelled.");
        }

        await UpdateOperationStatusAsync(connection, transaction, context.Id, OperationStatuses.Cancelled, cancellationToken);
        return "Operation cancelled.";
    }

    private async Task<OperationContext> LoadOperationContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long operationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select
                id,
                reference,
                operation_type,
                status,
                warehouse_id,
                from_location_id,
                to_location_id,
                contact_name,
                delivery_address,
                to_char(schedule_date, 'YYYY-MM-DD') as schedule_date,
                responsible_user_id,
                notes
            from inventory_operation
            where id = @id;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("id", operationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Operation was not found.");
        }

        var context = new OperationContext
        {
            Id = reader.GetInt64(0),
            Reference = reader.GetString(1),
            Type = reader.GetString(2),
            Status = reader.GetString(3),
            WarehouseId = reader.GetInt64(4),
            FromLocationId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            ToLocationId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
            ContactName = reader.GetString(7),
            DeliveryAddress = reader.GetString(8),
            ScheduleDate = reader.GetString(9),
            ResponsibleUserId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            Notes = reader.GetString(11)
        };

        await reader.CloseAsync();

        await using var linesCommand = new NpgsqlCommand(
            """
            select l.id, l.product_id, l.quantity, l.unit_cost, l.note, p.sku, p.name
            from inventory_operation_line l
            join product p on p.id = l.product_id
            where operation_id = @operationId
            order by l.id asc;
            """,
            connection,
            transaction);
        linesCommand.Parameters.AddWithValue("operationId", operationId);

        await using var linesReader = await linesCommand.ExecuteReaderAsync(cancellationToken);
        while (await linesReader.ReadAsync(cancellationToken))
        {
            context.Lines.Add(new OperationLineContext
            {
                Id = linesReader.GetInt64(0),
                ProductId = linesReader.GetInt64(1),
                Quantity = linesReader.GetInt32(2),
                UnitCost = linesReader.GetDecimal(3),
                Note = linesReader.GetString(4),
                ProductLabel = $"[{linesReader.GetString(5)}] {linesReader.GetString(6)}"
            });
        }

        return context;
    }

    private async Task<bool> HasStockShortageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Type == OperationTypes.Receipt)
        {
            return false;
        }

        if (!context.FromLocationId.HasValue)
        {
            return true;
        }

        foreach (var line in context.Lines)
        {
            var available = await GetAvailableStockAsync(
                connection,
                transaction,
                context.WarehouseId,
                context.FromLocationId.Value,
                line.ProductId,
                cancellationToken);

            if (available < line.Quantity)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<int> GetAvailableStockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long warehouseId,
        long locationId,
        long productId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select coalesce(on_hand - allocated, 0)
            from stock_balance
            where warehouse_id = @warehouseId
              and location_id = @locationId
              and product_id = @productId;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("warehouseId", warehouseId);
        command.Parameters.AddWithValue("locationId", locationId);
        command.Parameters.AddWithValue("productId", productId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private async Task ApplyOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OperationContext context,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        switch (context.Type)
        {
            case var receipt when receipt == OperationTypes.Receipt:
                if (!context.ToLocationId.HasValue)
                {
                    throw new InvalidOperationException("Receipt operations require a destination location.");
                }

                foreach (var line in context.Lines)
                {
                    await UpsertBalanceAsync(connection, transaction, context.WarehouseId, context.ToLocationId.Value, line.ProductId, line.Quantity, cancellationToken);
                    await InsertStockMoveAsync(
                        connection,
                        transaction,
                        context.Reference,
                        context.WarehouseId,
                        line.ProductId,
                        null,
                        context.ToLocationId,
                        "IN",
                        line.Quantity,
                        currentUserId,
                        line.Note,
                        context.Id,
                        line.Id,
                        cancellationToken);
                }

                break;

            case var delivery when delivery == OperationTypes.Delivery:
                if (!context.FromLocationId.HasValue)
                {
                    throw new InvalidOperationException("Delivery operations require a source location.");
                }

                foreach (var line in context.Lines)
                {
                    await UpsertBalanceAsync(connection, transaction, context.WarehouseId, context.FromLocationId.Value, line.ProductId, -line.Quantity, cancellationToken);
                    await InsertStockMoveAsync(
                        connection,
                        transaction,
                        context.Reference,
                        context.WarehouseId,
                        line.ProductId,
                        context.FromLocationId,
                        null,
                        "OUT",
                        line.Quantity,
                        currentUserId,
                        line.Note,
                        context.Id,
                        line.Id,
                        cancellationToken);
                }

                break;

            default:
                if (!context.FromLocationId.HasValue || !context.ToLocationId.HasValue)
                {
                    throw new InvalidOperationException("Adjustment operations require both source and destination locations.");
                }

                foreach (var line in context.Lines)
                {
                    await UpsertBalanceAsync(connection, transaction, context.WarehouseId, context.FromLocationId.Value, line.ProductId, -line.Quantity, cancellationToken);
                    await UpsertBalanceAsync(connection, transaction, context.WarehouseId, context.ToLocationId.Value, line.ProductId, line.Quantity, cancellationToken);
                    await InsertStockMoveAsync(
                        connection,
                        transaction,
                        context.Reference,
                        context.WarehouseId,
                        line.ProductId,
                        context.FromLocationId,
                        context.ToLocationId,
                        "ADJUST",
                        line.Quantity,
                        currentUserId,
                        line.Note,
                        context.Id,
                        line.Id,
                        cancellationToken);
                }

                break;
        }
    }

    private async Task UpsertBalanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long warehouseId,
        long locationId,
        long productId,
        int quantityDelta,
        CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            select id, on_hand
            from stock_balance
            where warehouse_id = @warehouseId
              and location_id = @locationId
              and product_id = @productId;
            """,
            connection,
            transaction);

        select.Parameters.AddWithValue("warehouseId", warehouseId);
        select.Parameters.AddWithValue("locationId", locationId);
        select.Parameters.AddWithValue("productId", productId);

        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var balanceId = reader.GetInt64(0);
            var onHand = reader.GetInt32(1);
            var newOnHand = onHand + quantityDelta;

            await reader.CloseAsync();

            if (newOnHand < 0)
            {
                throw new InvalidOperationException("Stock is not sufficient for this movement.");
            }

            await using var update = new NpgsqlCommand(
                """
                update stock_balance
                set on_hand = @onHand,
                    updated_at = now()
                where id = @id;
                """,
                connection,
                transaction);
            update.Parameters.AddWithValue("onHand", newOnHand);
            update.Parameters.AddWithValue("id", balanceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        await reader.CloseAsync();

        if (quantityDelta < 0)
        {
            throw new InvalidOperationException("Stock is not sufficient for this movement.");
        }

        await using var insert = new NpgsqlCommand(
            """
            insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated, updated_at)
            values (@warehouseId, @locationId, @productId, @onHand, 0, now());
            """,
            connection,
            transaction);
        insert.Parameters.AddWithValue("warehouseId", warehouseId);
        insert.Parameters.AddWithValue("locationId", locationId);
        insert.Parameters.AddWithValue("productId", productId);
        insert.Parameters.AddWithValue("onHand", quantityDelta);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureLocationWarehouseMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long warehouseId,
        long locationId,
        string fieldName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select warehouse_id from inventory_location where id = @locationId;",
            connection,
            transaction);
        command.Parameters.AddWithValue("locationId", locationId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            throw new InvalidOperationException($"{fieldName} was not found.");
        }

        if (Convert.ToInt64(result) != warehouseId)
        {
            throw new InvalidOperationException($"{fieldName} must belong to the selected warehouse.");
        }
    }

    private async Task InsertStockMoveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string reference,
        long warehouseId,
        long productId,
        long? fromLocationId,
        long? toLocationId,
        string moveKind,
        int quantity,
        long userId,
        string note,
        long? operationId,
        long? operationLineId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into stock_move
            (
                operation_id,
                operation_line_id,
                reference,
                product_id,
                warehouse_id,
                from_location_id,
                to_location_id,
                quantity,
                move_kind,
                event_at,
                performed_by_user_id,
                note
            )
            values
            (
                @operationId,
                @operationLineId,
                @reference,
                @productId,
                @warehouseId,
                @fromLocationId,
                @toLocationId,
                @quantity,
                @moveKind,
                now(),
                @userId,
                @note
            );
            """,
            connection,
            transaction);

        AddNullableBigint(command, "operationId", operationId);
        AddNullableBigint(command, "operationLineId", operationLineId);
        command.Parameters.AddWithValue("reference", reference);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("warehouseId", warehouseId);
        AddNullableBigint(command, "fromLocationId", fromLocationId);
        AddNullableBigint(command, "toLocationId", toLocationId);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("moveKind", moveKind);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("note", string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateOperationStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long operationId,
        string status,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            update inventory_operation
            set status = @status,
                updated_at = now()
            where id = @id;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("id", operationId);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string> GenerateReferenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long warehouseId,
        string operationType,
        CancellationToken cancellationToken)
    {
        await using var warehouseCommand = new NpgsqlCommand(
            "select code from warehouse where id = @warehouseId;",
            connection,
            transaction);
        warehouseCommand.Parameters.AddWithValue("warehouseId", warehouseId);

        var warehouseCode = Convert.ToString(await warehouseCommand.ExecuteScalarAsync(cancellationToken))
                            ?? throw new InvalidOperationException("Warehouse was not found.");

        await using var sequenceCommand = new NpgsqlCommand(
            "select nextval('operation_reference_seq');",
            connection,
            transaction);
        var sequenceValue = Convert.ToInt64(await sequenceCommand.ExecuteScalarAsync(cancellationToken));

        return $"{warehouseCode}/{OperationTypes.ToReferenceCode(operationType)}/{sequenceValue:0000}";
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed.Date;
        }

        throw new InvalidOperationException("Schedule date is invalid.");
    }

    private static void ValidateOperationEditor(OperationEditorViewModel model)
    {
        if (model.WarehouseId <= 0)
        {
            throw new InvalidOperationException("Warehouse is required.");
        }

        if (string.IsNullOrWhiteSpace(model.ContactName))
        {
            throw new InvalidOperationException("Contact is required.");
        }

        var activeLines = model.Lines.Where(line => line.ProductId > 0).ToList();
        if (activeLines.Count == 0)
        {
            throw new InvalidOperationException("At least one product line is required.");
        }

        if (model.Type == OperationTypes.Receipt && !model.ToLocationId.HasValue)
        {
            throw new InvalidOperationException("Receipt operations require a destination location.");
        }

        if (model.Type == OperationTypes.Delivery && !model.FromLocationId.HasValue)
        {
            throw new InvalidOperationException("Delivery operations require a source location.");
        }

        if (model.Type == OperationTypes.Adjustment && (!model.FromLocationId.HasValue || !model.ToLocationId.HasValue))
        {
            throw new InvalidOperationException("Adjustment operations require both source and destination locations.");
        }

        if (model.Type == OperationTypes.Adjustment
            && model.FromLocationId.HasValue
            && model.ToLocationId.HasValue
            && model.FromLocationId == model.ToLocationId)
        {
            throw new InvalidOperationException("Adjustment source and destination must be different.");
        }
    }

    private static void ValidateProductForm(ProductFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Sku))
        {
            throw new InvalidOperationException("SKU is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new InvalidOperationException("Product name is required.");
        }
    }

    private static void ValidateWarehouseForm(WarehouseFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            throw new InvalidOperationException("Warehouse code is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new InvalidOperationException("Warehouse name is required.");
        }
    }

    private static void ValidateLocationForm(LocationFormViewModel model)
    {
        if (model.WarehouseId <= 0)
        {
            throw new InvalidOperationException("Warehouse is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Code))
        {
            throw new InvalidOperationException("Location code is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new InvalidOperationException("Location name is required.");
        }
    }

    private static async Task EnsureProductSkuAvailableAsync(
        NpgsqlConnection connection,
        long? productId,
        string sku,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select 1
            from product
            where upper(sku) = upper(@sku)
              and (@id is null or id <> @id)
            limit 1;
            """,
            connection);

        command.Parameters.AddWithValue("sku", sku.Trim());
        AddNullableBigint(command, "id", productId);

        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
        {
            throw new InvalidOperationException("SKU already exists.");
        }
    }

    private static async Task EnsureWarehouseCodeAvailableAsync(
        NpgsqlConnection connection,
        long? warehouseId,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select 1
            from warehouse
            where upper(code) = upper(@code)
              and (@id is null or id <> @id)
            limit 1;
            """,
            connection);

        command.Parameters.AddWithValue("code", code.Trim());
        AddNullableBigint(command, "id", warehouseId);

        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
        {
            throw new InvalidOperationException("Warehouse code already exists.");
        }
    }

    private static async Task EnsureLocationCodeAvailableAsync(
        NpgsqlConnection connection,
        long? locationId,
        long warehouseId,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select 1
            from inventory_location
            where warehouse_id = @warehouseId
              and upper(code) = upper(@code)
              and (@id is null or id <> @id)
            limit 1;
            """,
            connection);

        command.Parameters.AddWithValue("warehouseId", warehouseId);
        command.Parameters.AddWithValue("code", code.Trim());
        AddNullableBigint(command, "id", locationId);

        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
        {
            throw new InvalidOperationException("Location code already exists in that warehouse.");
        }
    }

    private async Task UpdateProductAsync(
        NpgsqlConnection connection,
        ProductFormViewModel model,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            update product
            set sku = @sku,
                name = @name,
                unit_cost = @unitCost,
                updated_at = now()
            where id = @id;
            """,
            connection);

        command.Parameters.AddWithValue("id", model.Id!.Value);
        command.Parameters.AddWithValue("sku", model.Sku.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("name", model.Name.Trim());
        command.Parameters.AddWithValue("unitCost", model.UnitCost);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertProductAsync(
        NpgsqlConnection connection,
        ProductFormViewModel model,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            """
            insert into product (sku, name, unit_cost, created_at, updated_at)
            values (@sku, @name, @unitCost, now(), now());
            """,
            connection);
        insert.Parameters.AddWithValue("sku", model.Sku.Trim().ToUpperInvariant());
        insert.Parameters.AddWithValue("name", model.Name.Trim());
        insert.Parameters.AddWithValue("unitCost", model.UnitCost);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateWarehouseAsync(
        NpgsqlConnection connection,
        WarehouseFormViewModel model,
        CancellationToken cancellationToken)
    {
        await using var update = new NpgsqlCommand(
            """
            update warehouse
            set code = @code,
                name = @name,
                address = @address,
                updated_at = now()
            where id = @id;
            """,
            connection);
        update.Parameters.AddWithValue("id", model.Id!.Value);
        update.Parameters.AddWithValue("code", model.Code.Trim().ToUpperInvariant());
        update.Parameters.AddWithValue("name", model.Name.Trim());
        update.Parameters.AddWithValue("address", model.Address.Trim());
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertWarehouseAsync(
        NpgsqlConnection connection,
        WarehouseFormViewModel model,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            """
            insert into warehouse (code, name, address, created_at, updated_at)
            values (@code, @name, @address, now(), now());
            """,
            connection);
        insert.Parameters.AddWithValue("code", model.Code.Trim().ToUpperInvariant());
        insert.Parameters.AddWithValue("name", model.Name.Trim());
        insert.Parameters.AddWithValue("address", model.Address.Trim());
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateLocationAsync(
        NpgsqlConnection connection,
        LocationFormViewModel model,
        CancellationToken cancellationToken)
    {
        await using var update = new NpgsqlCommand(
            """
            update inventory_location
            set warehouse_id = @warehouseId,
                code = @code,
                name = @name,
                kind = @kind,
                updated_at = now()
            where id = @id;
            """,
            connection);
        update.Parameters.AddWithValue("id", model.Id!.Value);
        update.Parameters.AddWithValue("warehouseId", model.WarehouseId);
        update.Parameters.AddWithValue("code", model.Code.Trim().ToUpperInvariant());
        update.Parameters.AddWithValue("name", model.Name.Trim());
        update.Parameters.AddWithValue("kind", string.IsNullOrWhiteSpace(model.Kind) ? "Stock" : model.Kind.Trim());
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertLocationAsync(
        NpgsqlConnection connection,
        LocationFormViewModel model,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            """
            insert into inventory_location (warehouse_id, code, name, kind, created_at, updated_at)
            values (@warehouseId, @code, @name, @kind, now(), now());
            """,
            connection);
        insert.Parameters.AddWithValue("warehouseId", model.WarehouseId);
        insert.Parameters.AddWithValue("code", model.Code.Trim().ToUpperInvariant());
        insert.Parameters.AddWithValue("name", model.Name.Trim());
        insert.Parameters.AddWithValue("kind", string.IsNullOrWhiteSpace(model.Kind) ? "Stock" : model.Kind.Trim());
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullableBigint(NpgsqlCommand command, string name, long? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Bigint);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }

    private sealed class OperationContext
    {
        public long Id { get; init; }

        public string Reference { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public long WarehouseId { get; init; }

        public long? FromLocationId { get; init; }

        public long? ToLocationId { get; init; }

        public string ContactName { get; init; } = string.Empty;

        public string DeliveryAddress { get; init; } = string.Empty;

        public string ScheduleDate { get; init; } = string.Empty;

        public long? ResponsibleUserId { get; init; }

        public string Notes { get; init; } = string.Empty;

        public List<OperationLineContext> Lines { get; } = [];
    }

    private sealed class OperationLineContext
    {
        public long Id { get; init; }

        public long ProductId { get; init; }

        public int Quantity { get; init; }

        public decimal UnitCost { get; init; }

        public string Note { get; init; } = string.Empty;

        public string ProductLabel { get; init; } = string.Empty;
    }
}
