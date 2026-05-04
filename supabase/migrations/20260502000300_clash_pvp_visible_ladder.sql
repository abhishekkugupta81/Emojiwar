alter table public.mode_ratings
add column if not exists hidden_mmr integer not null default 1000,
add column if not exists visible_points integer not null default 0,
add column if not exists last_visible_points_change_at timestamptz,
add column if not exists matches_vs_humans integer not null default 0,
add column if not exists matches_vs_bots integer not null default 0,
add column if not exists bot_fill_wins integer not null default 0,
add column if not exists bot_fill_losses integer not null default 0,
add column if not exists bot_fill_points_earned integer not null default 0;

update public.mode_ratings
set hidden_mmr = rating
where ladder_mode = 'emoji_clash_pvp'
  and hidden_mmr = 1000
  and rating <> 1000;

update public.mode_ratings
set last_visible_points_change_at = coalesce(last_visible_points_change_at, created_at)
where ladder_mode = 'emoji_clash_pvp';

alter table public.mode_rating_events
add column if not exists opponent_type text not null default 'human',
add column if not exists old_hidden_mmr integer,
add column if not exists new_hidden_mmr integer,
add column if not exists hidden_mmr_delta integer,
add column if not exists old_visible_points integer,
add column if not exists new_visible_points integer,
add column if not exists visible_delta integer,
add column if not exists bot_profile_id text;

create index if not exists mode_ratings_clash_visible_points_lookup
on public.mode_ratings (ladder_mode, visible_points desc, last_visible_points_change_at asc, user_id asc);

create index if not exists mode_rating_events_clash_bot_daily_lookup
on public.mode_rating_events (user_id, ladder_mode, opponent_type, result, created_at desc);
