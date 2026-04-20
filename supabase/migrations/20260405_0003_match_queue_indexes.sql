create index if not exists matches_ranked_queue_lookup
on public.matches (created_at)
where mode = 'pvp_ranked' and status = 'queued' and player_b is null;

create index if not exists matches_participant_updated_lookup
on public.matches (updated_at desc, player_a, player_b);
