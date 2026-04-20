alter table public.decks
add column if not exists is_active boolean not null default false;

create unique index if not exists decks_one_active_per_user
on public.decks (user_id)
where is_active;

with ranked_decks as (
  select
    id,
    user_id,
    row_number() over (partition by user_id order by created_at asc, id asc) as deck_rank
  from public.decks
),
users_without_active_deck as (
  select distinct ranked_decks.user_id
  from ranked_decks
  where not exists (
    select 1
    from public.decks existing_decks
    where existing_decks.user_id = ranked_decks.user_id
      and existing_decks.is_active
  )
)
update public.decks
set is_active = true
from ranked_decks
join users_without_active_deck on users_without_active_deck.user_id = ranked_decks.user_id
where public.decks.id = ranked_decks.id
  and ranked_decks.deck_rank = 1;
