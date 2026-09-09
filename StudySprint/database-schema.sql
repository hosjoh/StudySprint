create schema if not exists private;

-- =====================================================
-- PROFILES
-- =====================================================

create table public.profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    display_name varchar(100) not null default '',
    created_at timestamptz not null default now()
);

-- =====================================================
-- SUBJECTS
-- =====================================================

create table public.subjects (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    name varchar(80) not null,
    created_at timestamptz not null default now(),

    constraint subjects_name_length
        check (char_length(trim(name)) between 1 and 80)
);

-- =====================================================
-- STUDY SESSIONS
-- =====================================================

create table public.study_sessions (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    subject_id uuid references public.subjects(id) on delete set null,
    goal varchar(200) not null,
    session_date date not null default current_date,
    duration_minutes integer not null default 25,
    completed boolean not null default false,
    created_at timestamptz not null default now(),

    constraint study_sessions_goal_length
        check (char_length(trim(goal)) between 1 and 200),

    constraint study_sessions_duration
        check (duration_minutes between 5 and 240)
);

-- =====================================================
-- INDEXES
-- =====================================================

create index subjects_user_id_idx
    on public.subjects(user_id);

create index study_sessions_user_id_idx
    on public.study_sessions(user_id);

create index study_sessions_subject_id_idx
    on public.study_sessions(subject_id);

-- =====================================================
-- AUTOMATIC PROFILE CREATION
-- =====================================================

create or replace function private.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.profiles (id, display_name)
    values (
        new.id,
        coalesce(
            new.raw_user_meta_data ->> 'display_name',
            split_part(new.email, '@', 1)
        )
    );

    return new;
end;
$$;

create trigger on_auth_user_created
after insert on auth.users
for each row
execute procedure private.handle_new_user();

-- =====================================================
-- ROW LEVEL SECURITY
-- =====================================================

alter table public.profiles enable row level security;
alter table public.subjects enable row level security;
alter table public.study_sessions enable row level security;

-- Remove access from signed-out visitors.
revoke all on public.profiles from anon;
revoke all on public.subjects from anon;
revoke all on public.study_sessions from anon;

-- Only give logged-in users the operations StudySprint needs.
revoke all on public.profiles from authenticated;
revoke all on public.subjects from authenticated;
revoke all on public.study_sessions from authenticated;

grant select, update
on public.profiles
to authenticated;

grant select, insert, update, delete
on public.subjects
to authenticated;

grant select, insert, update, delete
on public.study_sessions
to authenticated;

-- =====================================================
-- PROFILE POLICIES
-- =====================================================

create policy "Users can view their own profile"
on public.profiles
for select
to authenticated
using ((select auth.uid()) = id);

create policy "Users can update their own profile"
on public.profiles
for update
to authenticated
using ((select auth.uid()) = id)
with check ((select auth.uid()) = id);

-- =====================================================
-- SUBJECT POLICIES
-- =====================================================

create policy "Users can view their own subjects"
on public.subjects
for select
to authenticated
using ((select auth.uid()) = user_id);

create policy "Users can create their own subjects"
on public.subjects
for insert
to authenticated
with check ((select auth.uid()) = user_id);

create policy "Users can update their own subjects"
on public.subjects
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

create policy "Users can delete their own subjects"
on public.subjects
for delete
to authenticated
using ((select auth.uid()) = user_id);

-- =====================================================
-- STUDY SESSION POLICIES
-- =====================================================

create policy "Users can view their own study sessions"
on public.study_sessions
for select
to authenticated
using ((select auth.uid()) = user_id);

create policy "Users can create their own study sessions"
on public.study_sessions
for insert
to authenticated
with check ((select auth.uid()) = user_id);

create policy "Users can update their own study sessions"
on public.study_sessions
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

create policy "Users can delete their own study sessions"
on public.study_sessions
for delete
to authenticated
using ((select auth.uid()) = user_id);