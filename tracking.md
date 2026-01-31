# Product Analytics Architecture

## Overview
This document explains how product analytics tracking works in SubashaVentures.

## Data Flow

### 1. View & Click Events (Client → Edge Function → Database)
**Tracked by:** Client-side batching
**Process:**
1. User views/clicks product
2. `ProductInteractionService` batches events locally
3. When batch reaches 75 events OR user triggers flush
4. Edge function `update-product-analytics` is called
5. Edge function updates `product_analytics` and `products` tables

**Why client-side batching?**
- Reduces edge function calls
- Better performance
- Handles offline scenarios
- Reduces database load

### 2. Add to Cart Events (Database Trigger)
**Tracked by:** `trigger_update_analytics_from_cart`
**Process:**
1. User adds product to cart
2. `CartService` updates `cart` table
3. Database trigger automatically increments `product_analytics.total_add_to_cart`
4. Also updates `products.add_to_cart_count`

**Why database trigger?**
- Guaranteed accuracy
- No duplicate tracking
- Works even if client-side fails
- Atomic operation

### 3. Wishlist Events (Database Trigger)
**Tracked by:** `trigger_update_analytics_from_wishlist`
**Process:**
1. User adds product to wishlist
2. `WishlistService` updates `wishlist` table
3. Database trigger automatically tracks wishlist addition
4. Updates `product_analytics.updated_at`

**Why database trigger?**
- Same benefits as cart trigger
- Centralized tracking logic

### 4. Purchase Events (Database Trigger)
**Tracked by:** `trigger_update_analytics_from_order`
**Process:**
1. Order is marked as paid in `orders` table
2. Database trigger automatically:
   - Increments `product_analytics.total_purchases`
   - Adds to `product_analytics.total_revenue`
   - Updates `products.purchase_count`
   - Updates `products.total_revenue`
   - Recalculates conversion rates

**Why database trigger?**
- Guaranteed accuracy for financial data
- Atomic with order completion
- No risk of missed purchases
- Automatic stock deduction

## Architecture Decisions

### Client-Side Batching (View/Click)
**Pros:**
- Reduced API calls
- Better performance
- Works offline
- User gets immediate feedback

**Cons:**
- Slightly delayed analytics
- Requires localStorage
- Must handle flush failures

### Database Triggers (Cart/Wishlist/Purchase)
**Pros:**
- 100% accurate
- No client-side logic needed
- Works even if client fails
- Atomic operations
- Single source of truth

**Cons:**
- Harder to debug
- Performance impact on writes
- Can't be easily disabled

## Analytics Tables

### `products` table
- Stores raw counters: `view_count`, `click_count`, `add_to_cart_count`, `purchase_count`, `sales_count`, `total_revenue`
- Updated by both edge function (View/Click) and triggers (Cart/Wishlist/Purchase)

### `product_analytics` table
- Stores detailed analytics with conversion rates
- Calculated fields: `view_to_cart_rate`, `cart_to_purchase_rate`, `overall_conversion_rate`
- Performance indicators: `is_trending`, `is_best_seller`, `needs_attention`
- Updated by both edge function (View/Click) and triggers (Cart/Wishlist/Purchase)

## Conversion Rate Calculations

All conversion rates are automatically calculated:
```sql
view_to_cart_rate = (total_add_to_cart / total_views) * 100
cart_to_purchase_rate = (total_purchases / total_add_to_cart) * 100
overall_conversion_rate = (total_purchases / total_views) * 100
```

These are recalculated:
- When edge function processes View/Click batch
- When cart trigger fires (add to cart)
- When order trigger fires (purchase)

## Usage in Code

### Tracking a View
```csharp
await _productInteractionService.TrackViewAsync(productId, userId);
// Batched locally, sent to edge function when batch is full
```

### Tracking a Click
```csharp
await _productInteractionService.TrackClickAsync(productId, userId);
// Batched locally, sent to edge function when batch is full
```

### Adding to Cart (Automatic)
```csharp
await _cartService.AddItemAsync(cartItem);
// Database trigger automatically updates analytics - NO manual tracking needed
```

### Adding to Wishlist (Automatic)
```csharp
await _wishlistService.AddItemAsync(wishlistItem);
// Database trigger automatically updates analytics - NO manual tracking needed
```

### Completing Purchase (Automatic)
```csharp
await _orderService.MarkAsPaidAsync(orderId);
// Database trigger automatically:
// - Updates purchase count
// - Adds revenue
// - Deducts stock
// - Recalculates conversion rates
// NO manual tracking needed
```

## Manual Flush
Force flush pending View/Click events:
```csharp
await _productInteractionService.FlushPendingInteractionsAsync();
```

## Performance Indicators
Updated hourly via scheduled job:
- `is_trending`: Views increased >50% in last 7 days
- `is_best_seller`: Top 10% by purchases
- `needs_attention`: High views but low conversion

## Security
- RLS enabled on all tables
- Only `superior_admin` can write to `products` and `product_analytics` directly
- Edge functions use `service_role` key to bypass RLS
- Database triggers run with `SECURITY DEFINER` (elevated privileges)

## Monitoring
Check analytics health:
```sql
SELECT public.get_analytics_health_check();
```

Get system metrics:
```sql
SELECT public.get_system_metrics();
```

Verify setup:
```sql
SELECT public.verify_system_setup();
```
