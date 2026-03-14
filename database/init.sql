drop table if exists stock_move cascade;
drop table if exists stock_balance cascade;
drop table if exists inventory_operation_line cascade;
drop table if exists inventory_operation cascade;
drop table if exists product cascade;
drop table if exists inventory_location cascade;
drop table if exists warehouse cascade;
drop table if exists app_user cascade;
drop sequence if exists operation_reference_seq;

create sequence operation_reference_seq start 1 increment 1;

create table app_user (
    id bigint generated always as identity primary key,
    login_id varchar(12) not null,
    display_name varchar(80) not null,
    email varchar(255) not null,
    password_hash text not null,
    is_active boolean not null default true,
    email_verified_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index ux_app_user_login_id on app_user (lower(login_id));
create unique index ux_app_user_email on app_user (lower(email));

create table pending_signup (
    id bigint generated always as identity primary key,
    login_id varchar(12) not null,
    display_name varchar(80) not null,
    email varchar(255) not null,
    password_hash text not null,
    otp_hash text not null,
    otp_expires_at timestamptz not null,
    attempts integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index ux_pending_signup_login_id on pending_signup (lower(login_id));
create unique index ux_pending_signup_email on pending_signup (lower(email));

create table warehouse (
    id bigint generated always as identity primary key,
    code varchar(10) not null unique,
    name varchar(80) not null,
    address varchar(240) not null default '',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table inventory_location (
    id bigint generated always as identity primary key,
    warehouse_id bigint not null references warehouse(id) on delete cascade,
    code varchar(30) not null,
    name varchar(80) not null,
    kind varchar(20) not null default 'Stock',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (warehouse_id, code)
);

create table product (
    id bigint generated always as identity primary key,
    sku varchar(32) not null unique,
    name varchar(120) not null,
    unit_cost numeric(12,2) not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table inventory_operation (
    id bigint generated always as identity primary key,
    reference varchar(32) not null unique,
    operation_type varchar(20) not null check (operation_type in ('Receipt', 'Delivery', 'Adjustment')),
    status varchar(20) not null check (status in ('Draft', 'Waiting', 'Ready', 'Done', 'Cancelled')),
    warehouse_id bigint not null references warehouse(id),
    from_location_id bigint null references inventory_location(id),
    to_location_id bigint null references inventory_location(id),
    contact_name varchar(120) not null,
    delivery_address varchar(240) not null default '',
    schedule_date date not null,
    responsible_user_id bigint null references app_user(id),
    notes text not null default '',
    created_by_user_id bigint not null references app_user(id),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table inventory_operation_line (
    id bigint generated always as identity primary key,
    operation_id bigint not null references inventory_operation(id) on delete cascade,
    product_id bigint not null references product(id),
    quantity integer not null check (quantity > 0),
    unit_cost numeric(12,2) not null default 0,
    note varchar(240) not null default ''
);

create table stock_balance (
    id bigint generated always as identity primary key,
    warehouse_id bigint not null references warehouse(id) on delete cascade,
    location_id bigint not null references inventory_location(id) on delete cascade,
    product_id bigint not null references product(id) on delete cascade,
    on_hand integer not null default 0,
    allocated integer not null default 0,
    updated_at timestamptz not null default now(),
    unique (warehouse_id, location_id, product_id)
);

create table stock_move (
    id bigint generated always as identity primary key,
    operation_id bigint null references inventory_operation(id) on delete set null,
    operation_line_id bigint null references inventory_operation_line(id) on delete set null,
    reference varchar(32) not null,
    product_id bigint not null references product(id),
    warehouse_id bigint not null references warehouse(id),
    from_location_id bigint null references inventory_location(id),
    to_location_id bigint null references inventory_location(id),
    quantity integer not null check (quantity > 0),
    move_kind varchar(10) not null check (move_kind in ('IN', 'OUT', 'ADJUST')),
    event_at timestamptz not null default now(),
    performed_by_user_id bigint null references app_user(id),
    note varchar(240) not null default ''
);

insert into app_user (login_id, display_name, email, password_hash, is_active, email_verified_at)
values
    ('admin', 'Admin Operator', 'admin@coreinventory.local', 'PBKDF2-SHA1$100000$thKajyASIe5uvAExxEeMVg==$BD+3XzeGtgfa/lkGHUB2MK7HVJE4UoWw7+mT5WZ9YyU=', true, now());

insert into warehouse (code, name, address)
values
    ('WH', 'Main Warehouse', 'Plot 12, Industrial Area, Pune'),
    ('BLR', 'Bangalore Hub', 'Whitefield Logistics Corridor, Bengaluru');

insert into inventory_location (warehouse_id, code, name, kind)
values
    (1, 'RCV', 'Receiving Dock', 'Inbound'),
    (1, 'STOCK1', 'Stock Room 1', 'Stock'),
    (1, 'STOCK2', 'Stock Room 2', 'Stock'),
    (1, 'DSP', 'Dispatch Bay', 'Outbound'),
    (2, 'STOCK1', 'Bangalore Rack 1', 'Stock');

insert into product (sku, name, unit_cost)
values
    ('DESK001', 'Desk', 3000.00),
    ('TABLE001', 'Table', 3000.00),
    ('CHAIR001', 'Chair', 1200.00),
    ('LAMP001', 'Lamp', 850.00);

insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated)
values
    (1, 2, 1, 45, 0),
    (1, 3, 2, 50, 0),
    (1, 2, 3, 10, 0),
    (1, 2, 4, 6, 0),
    (2, 5, 1, 8, 0);

insert into inventory_operation
    (reference, operation_type, status, warehouse_id, from_location_id, to_location_id, contact_name, delivery_address, schedule_date, responsible_user_id, notes, created_by_user_id)
values
    ('WH/IN/0001', 'Receipt', 'Ready', 1, null, 2, 'Azure Interior', '', current_date + 1, 1, 'Incoming desks', 1),
    ('WH/IN/0002', 'Receipt', 'Draft', 1, null, 3, 'Northwood Supply', '', current_date + 2, 1, 'Tables for next batch', 1),
    ('WH/OUT/0003', 'Delivery', 'Waiting', 1, 2, null, 'Orion Spaces', 'Site 8, Hinjawadi', current_date - 1, 1, 'Short stock expected', 1),
    ('WH/OUT/0004', 'Delivery', 'Ready', 1, 3, null, 'BlueArc Studio', 'Baner Road', current_date, 1, 'Ready to dispatch', 1),
    ('WH/IN/0005', 'Receipt', 'Done', 1, null, 2, 'Civic Furnishings', '', current_date - 7, 1, 'Seed history row', 1),
    ('WH/OUT/0006', 'Delivery', 'Done', 1, 2, null, 'Parklane Homes', 'Kharadi', current_date - 3, 1, 'Seed history row', 1);

insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note)
values
    (1, 1, 6, 3000.00, 'New desks'),
    (2, 2, 4, 3000.00, 'Tables pending QA'),
    (3, 1, 60, 3000.00, 'Desk shipment'),
    (4, 2, 4, 3000.00, 'Ready for delivery'),
    (5, 3, 10, 1200.00, 'Chairs received'),
    (6, 4, 2, 850.00, 'Lamps delivered');

insert into stock_move (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, event_at, performed_by_user_id, note)
values
    (5, 5, 'WH/IN/0005', 3, 1, null, 2, 10, 'IN', now() - interval '7 day', 1, 'Seed receipt move'),
    (6, 6, 'WH/OUT/0006', 4, 1, 2, null, 2, 'OUT', now() - interval '3 day', 1, 'Seed delivery move'),
    (null, null, 'MANUAL-20260314090000', 1, 1, null, 2, 5, 'IN', now() - interval '1 day', 1, 'Manual correction');

select setval('operation_reference_seq', 6, true);
