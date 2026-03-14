-- Add role column with default 'user'
alter table app_user add column if not exists role varchar(20) not null default 'user';

-- Set admin role for the seed admin account
update app_user set role = 'admin' where login_id = 'admin';
