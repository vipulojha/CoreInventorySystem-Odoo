alter table app_user
    add column if not exists email_verified_at timestamptz null;

update app_user
set email_verified_at = coalesce(email_verified_at, now())
where login_id = 'admin';

create table if not exists pending_signup (
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

create unique index if not exists ux_pending_signup_login_id on pending_signup (lower(login_id));
create unique index if not exists ux_pending_signup_email on pending_signup (lower(email));
