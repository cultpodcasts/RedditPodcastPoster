# PR1 Stage Note: PushSubscriptions container support and V2 repository

## Change
- Added `PushSubscriptionsContainer` to `CosmosDbSettings`.
- Extended container factory API with `CreatePushSubscriptionsContainer()`.
- Implemented push-subscriptions factory method in `CosmosDbContainerFactory`.
- Added V2 abstraction and repository:
  - `IPushSubscriptionRepositoryV2`
  - `PushSubscriptionRepositoryV2`
- Registered `IPushSubscriptionRepositoryV2` in persistence DI using `CreatePushSubscriptionsContainer()`.

## Lookup alignment
- `lookup` container is used for single-item lookup documents such as `KnownTerms` and `EliminationTerms`.

## Purpose
- Ensure parallel infrastructure has an explicit push-subscriptions data path.
- Keep legacy repositories intact while enabling new-container routing.
