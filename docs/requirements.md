# Extracted Scope

This implementation was derived from the Excalidraw workflow supplied with the problem statement.

## Authentication

- Login with login ID and password
- Show invalid credentials message on failure
- Sign up with unique login ID and unique email
- Enforce password policy: upper, lower, special, 8+ chars
- Provide a forgot-password flow

## Main Navigation

- Dashboard
- Operations
- Stock
- Move History
- Settings

## Operations

- Receipt
- Delivery
- Adjustment
- Auto-generated references in the format `<Warehouse>/<Operation>/<ID>`
- List view and kanban view
- Search by reference or contact
- Status flow:
  - Receipt: Draft -> Ready -> Done
  - Delivery: Draft -> Waiting -> Ready -> Done
- Flag low-stock delivery lines and move the operation to Waiting
- Print once done

## Inventory

- Maintain products with unit cost
- Maintain stock by warehouse and location
- Allow manual stock updates
- Show free-to-use stock
- Record move history with in/out style semantics

## Settings

- Warehouses
- Locations within warehouses
