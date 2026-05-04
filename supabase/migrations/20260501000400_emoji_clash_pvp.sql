alter type public.match_mode add value if not exists 'emoji_clash_pvp';

drop policy if exists "turns_select_participant" on public.turns;

create index if not exists matches_clash_queue_lookup
on public.matches (created_at)
where status = 'queued' and player_b is null;

create index if not exists matches_online_active_by_player_a
on public.matches (player_a, updated_at desc)
where status in ('queued', 'banning', 'formation', 'pick');

create index if not exists matches_online_active_by_player_b
on public.matches (player_b, updated_at desc)
where status in ('banning', 'formation', 'pick');
