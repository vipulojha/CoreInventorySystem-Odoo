-- Insert a wide variety of products
insert into product (sku, name, unit_cost) values
  ('BOOK001',  'Office Notebook (A4)',    120.00),
  ('PEN001',   'Ballpoint Pen (Pack 12)', 45.00),
  ('MONITOR01','24" LED Monitor',         8500.00),
  ('KEYBOARD1','Mechanical Keyboard',     2800.00),
  ('MOUSE001', 'Wireless Mouse',          950.00),
  ('HEADSET01','Noise Cancelling Headset',3200.00),
  ('WEBCAM001','1080p Webcam',            1600.00),
  ('CABLE001', 'HDMI Cable 2m',           299.00),
  ('CHARGER01','65W USB-C Charger',       799.00),
  ('LAPTOP001','Laptop Stand (Adjustable)',699.00),
  ('STOREBOX1','Storage Box (Large)',     350.00),
  ('SHELF001', 'Wall Shelf Unit',         1800.00),
  ('CABINET01','Filing Cabinet (3-drawer)',5500.00),
  ('WHITEBRD1','Whiteboard 4x3 ft',       2200.00),
  ('MARKER001','Whiteboard Markers (set)', 180.00),
  ('PROJCTR01','Mini Projector',          12000.00),
  ('SCREEN001','Projector Screen 80"',    3500.00),
  ('PAPER001', 'A4 Paper Ream (500 sheets)',280.00),
  ('STAPLER01','Heavy-Duty Stapler',      450.00),
  ('SCSSORS01','Office Scissors',         95.00),
  ('TAPE001',  'Packing Tape (6 rolls)',  210.00),
  ('BUBBLE001','Bubble Wrap Roll (50m)',   550.00),
  ('PALETTE01','Wooden Pallet',           800.00),
  ('FORKLIFT1','Electric Pallet Jack',    85000.00),
  ('HELMET001','Safety Helmet',           320.00),
  ('GLOVES001','Warehouse Gloves (pair)', 75.00),
  ('VEST001',  'Hi-Vis Safety Vest',      180.00),
  ('BARCODE01','Barcode Scanner (USB)',   3800.00),
  ('PRINTER01','Label Printer',           4500.00),
  ('LABELS001','Shipping Labels (100)',   120.00)
on conflict (sku) do nothing;

-- Add stock balances for new products (warehouse 1, location 2 = STOCK1)
insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated)
select 1, 2, p.id, floor(random()*80+5)::int, 0
from product p
where p.sku not in ('DESK001','TABLE001','CHAIR001','LAMP001')
on conflict (warehouse_id, location_id, product_id) do nothing;

-- Add more operations for richer history
insert into inventory_operation
  (reference, operation_type, status, warehouse_id, from_location_id, to_location_id, contact_name, delivery_address, schedule_date, responsible_user_id, notes, created_by_user_id)
values
  ('WH/IN/0007', 'Receipt',    'Done',    1, null, 2, 'TechWorld Supplies', '',               current_date - 10, 1, 'Monitor and keyboard batch', 1),
  ('WH/IN/0008', 'Receipt',    'Done',    1, null, 2, 'SafetyFirst Co.',    '',               current_date - 8,  1, 'Safety gear restocking',    1),
  ('WH/OUT/0009','Delivery',   'Done',    1, 2,    null,'Acme Corp',        'MG Road, Pune',  current_date - 6,  1, 'Office setup delivery',     1),
  ('WH/OUT/0010','Delivery',   'Ready',   1, 3,    null,'BlueSky Offices',  'Kothrud, Pune',  current_date - 1,  1, 'Desk and chair order',      1),
  ('WH/IN/0011', 'Receipt',    'Ready',   1, null, 2, 'PrintMart Pvt Ltd', '',               current_date + 1,  1, 'Stationery bulk order',     1),
  ('WH/ADJ/0012','Adjustment', 'Done',    1, 2,    2, 'Internal',          '',               current_date - 3,  1, 'Cycle count correction',    1)
on conflict do nothing;

-- Add line items for new operations
insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note)
select op.id, p.id, 10, p.unit_cost, 'Batch shipment'
from inventory_operation op, product p
where op.reference = 'WH/IN/0007' and p.sku in ('MONITOR01','KEYBOARD1','MOUSE001')
on conflict do nothing;

insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note)
select op.id, p.id, 20, p.unit_cost, 'Safety stock'
from inventory_operation op, product p
where op.reference = 'WH/IN/0008' and p.sku in ('HELMET001','GLOVES001','VEST001')
on conflict do nothing;

-- Add stock moves for the new done operations
insert into stock_move (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, event_at, performed_by_user_id, note)
select op.id, ol.id, op.reference, ol.product_id, 1, null, 2, ol.quantity, 'IN', now() - interval '10 day', 1, 'Tech supplies incoming'
from inventory_operation op join inventory_operation_line ol on ol.operation_id = op.id
where op.reference = 'WH/IN/0007'
on conflict do nothing;

insert into stock_move (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, event_at, performed_by_user_id, note)
select op.id, ol.id, op.reference, ol.product_id, 1, null, 2, ol.quantity, 'IN', now() - interval '8 day', 1, 'Safety gear restocked'
from inventory_operation op join inventory_operation_line ol on ol.operation_id = op.id
where op.reference = 'WH/IN/0008'
on conflict do nothing;

-- Manual adjustment move
insert into stock_move (reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, event_at, performed_by_user_id, note)
select 'WH/ADJ/0012', p.id, 1, null, 2, 3, 'ADJUST', now() - interval '3 day', 1, 'Cycle count adjustment'
from product p where p.sku = 'CHAIR001'
on conflict do nothing;
