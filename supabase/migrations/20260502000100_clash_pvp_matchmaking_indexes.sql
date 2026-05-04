create index if not exists matches_clash_pvp_queue_created_lookup
on public.matches (created_at asc)
where mode = 'emoji_clash_pvp' and status = 'queued' and player_b is null;
