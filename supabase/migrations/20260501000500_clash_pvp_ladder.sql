alter table public.matches
add column if not exists ladder_applied_at timestamptz;

create table if not exists public.mode_ratings (
  user_id uuid not null references auth.users(id) on delete cascade,
  ladder_mode text not null,
  rating integer not null default 1000,
  games_played integer not null default 0,
  wins integer not null default 0,
  losses integer not null default 0,
  draws integer not null default 0,
  timeout_forfeits integer not null default 0,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  primary key (user_id, ladder_mode)
);

create table if not exists public.mode_rating_events (
  id uuid primary key default gen_random_uuid(),
  match_id uuid not null references public.matches(id) on delete cascade,
  user_id uuid not null references auth.users(id) on delete cascade,
  ladder_mode text not null,
  result text not null check (result in ('win', 'loss', 'draw', 'timeout_forfeit')),
  old_rating integer not null,
  new_rating integer not null,
  delta integer not null,
  created_at timestamptz not null default timezone('utc', now()),
  unique (match_id, user_id, ladder_mode)
);

drop trigger if exists set_mode_ratings_updated_at on public.mode_ratings;
create trigger set_mode_ratings_updated_at before update on public.mode_ratings
for each row execute procedure public.set_updated_at();

alter table public.mode_ratings enable row level security;
alter table public.mode_rating_events enable row level security;

drop policy if exists "mode_ratings_select_all" on public.mode_ratings;
create policy "mode_ratings_select_all" on public.mode_ratings
for select using (true);

drop policy if exists "mode_rating_events_select_own" on public.mode_rating_events;
create policy "mode_rating_events_select_own" on public.mode_rating_events
for select using (auth.uid() = user_id);

create index if not exists mode_ratings_ladder_lookup
on public.mode_ratings (ladder_mode, rating desc, updated_at asc);

create index if not exists mode_rating_events_match_lookup
on public.mode_rating_events (match_id);

create index if not exists mode_rating_events_user_lookup
on public.mode_rating_events (user_id, created_at desc);
