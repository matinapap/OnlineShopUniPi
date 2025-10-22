using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Helpers;
using OnlineShopUniPi.Models;
using static NuGet.Packaging.PackagingConstants;

namespace OnlineShopUniPi.Controllers
{
    public class OrdersController : Controller
    {
        private readonly OnlineStoreDBContext _context;

        public OrdersController(OnlineStoreDBContext context)
        {
            _context = context;
        }

        // Thank you page for a specific order
        [Authorize]
        public IActionResult ThankYou(int orderId)
        {
            // Retrieve order by ID
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound();
            }

            // Pass the order to the ThankYou view
            return View(order);
        }

        // Show orders that belong to the logged-in user's products
        [Authorize]
        public async Task<IActionResult> MyOrders(string filter = "Pending")
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var ordersQuery = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.OrderItems.Any(oi => oi.Product.UserId == userId));

            var orders = await ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var resultOrders = new List<Order>();

            foreach (var order in orders)
            {
                var userOrderItems = order.OrderItems
                    .Where(oi => oi.Product.UserId == userId)
                    .ToList();

                foreach (var item in userOrderItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Status))
                        item.Status = "Processing";
                }

                if (filter == "Pending")
                {
                    var pendingItems = userOrderItems
                        .Where(oi => oi.Status == "Processing")
                        .ToList();

                    if (pendingItems.Any())
                    {
                        resultOrders.Add(new Order
                        {
                            OrderId = order.OrderId,
                            UserId = order.UserId,
                            OrderDate = order.OrderDate,
                            ShippingAddress = order.ShippingAddress,
                            OrderItems = pendingItems,
                            OrderStatus = "Processing"
                        });
                    }
                }
                else if (filter == "History")
                {
                    var historyItems = userOrderItems
                        .Where(oi => oi.Status != "Processing")
                        .ToList();

                    foreach (var group in historyItems.GroupBy(oi => oi.Status))
                    {
                        resultOrders.Add(new Order
                        {
                            OrderId = order.OrderId,
                            UserId = order.UserId,
                            OrderDate = order.OrderDate,
                            ShippingAddress = order.ShippingAddress,
                            OrderItems = group.ToList(),
                            OrderStatus = group.Key
                        });
                    }
                }
            }

            ViewBag.Filter = filter;
            return View(resultOrders
                .OrderByDescending(o => o.OrderDate)
                .ToList());
        }


        [Authorize]
        public async Task<IActionResult> MyPurchases(string filter = "Pending")
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var purchasesQuery = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId)
                .Where(o => o.OrderItems.Any(oi => oi.Product.UserId != userId));

            var purchases = await purchasesQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var resultOrders = new List<Order>();

            foreach (var order in purchases)
            {
                // Keep only the products that are not owned by the user
                var userOrderItems = order.OrderItems
                    .Where(oi => oi.Product.UserId != userId)
                    .ToList();

                // Set default status if null
                foreach (var item in userOrderItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Status))
                        item.Status = "Processing";
                }

                if (filter == "Pending")
                {
                    // Pending: show only Processing items
                    var pendingItems = userOrderItems.Where(oi => oi.Status == "Processing").ToList();
                    if (pendingItems.Any())
                    {
                        resultOrders.Add(new Order
                        {
                            OrderId = order.OrderId,
                            UserId = order.UserId,
                            TotalPrice = order.TotalPrice,
                            OrderDate = order.OrderDate,
                            ShippingAddress = order.ShippingAddress,
                            OrderItems = pendingItems,
                            OrderStatus = "Processing"
                        });
                    }
                }
                else if (filter == "History")
                {
                    // History: group by Status, create separate Order "copies" per status
                    var grouped = userOrderItems
                        .Where(oi => oi.Status != "Processing")
                        .GroupBy(oi => oi.Status);

                    foreach (var group in grouped)
                    {
                        resultOrders.Add(new Order
                        {
                            OrderId = order.OrderId,
                            UserId = order.UserId,
                            TotalPrice = order.TotalPrice,
                            OrderDate = order.OrderDate,
                            ShippingAddress = order.ShippingAddress,
                            OrderItems = group.ToList(),
                            OrderStatus = group.Key
                        });
                    }
                }
            }

            ViewBag.Filter = filter;
            return View(resultOrders.OrderByDescending(o => o.OrderDate).ToList());
        }

        // Update order status (AJAX call with JSON body)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("OrderId", out var orderIdProp) ||
                    !data.TryGetProperty("Status", out var statusProp))
                {
                    return Json(new { success = false, message = "Invalid data." });
                }

                int orderId = orderIdProp.GetInt32();
                string newStatus = statusProp.GetString() ?? "";

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                    return Json(new { success = false, message = "Order not found." });

                // Update only the OrderItems of the current user
                foreach (var item in order.OrderItems.Where(oi => oi.Product.UserId == userId))
                {
                    item.Status = newStatus;

                    if (newStatus == "Cancelled" && item.Product != null)
                    {
                        item.Product.Quantity += item.Quantity;
                    }
                }

                // Recalculate the bundle status for this user
                var userStatuses = order.OrderItems
                    .Where(oi => oi.Product.UserId == userId)
                    .Select(oi => oi.Status)
                    .Distinct()
                    .ToList();

                if (userStatuses.Count == 1)
                    order.OrderStatus = userStatuses[0];
                else
                    order.OrderStatus = "Processing";

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // Checkout: create order and transaction
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Checkout(string paymentMethod)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users.FindAsync(userId);

            // Get shopping cart from session (productId -> quantity)
            var cart = HttpContext.Session.GetObjectFromJson<Dictionary<int, int>>("Cart")
                       ?? new Dictionary<int, int>();

            if (!cart.Any())
            {
                TempData["Error"] = "Το καλάθι είναι άδειο.";
                return RedirectToAction("Cart", "Products");
            }

            // Load products in the cart
            var productIds = cart.Keys.ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync();

            // Check if all products are still available
            if (products.Count != productIds.Count)
            {
                TempData["Error"] = "Κάποια προϊόντα δεν είναι διαθέσιμα.";
                return RedirectToAction("Cart", "Products");
            }

            decimal total = 0;
            var orderItems = new List<OrderItem>();

            // Validate stock and calculate totals
            foreach (var product in products)
            {
                var quantity = cart[product.ProductId];

                if (quantity > product.Quantity)
                {
                    TempData["Error"] = $"Δεν υπάρχει αρκετό από το προϊόν '{product.Title}'.";
                    return RedirectToAction("Cart", "Products");
                }

                // Reduce product stock
                product.Quantity -= quantity;

                var lineTotal = product.Price * quantity;
                total += lineTotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.ProductId,
                    Quantity = quantity,
                    Price = product.Price
                });
            }

            // Create new order
            var order = new Order
            {
                UserId = userId,
                TotalPrice = total,
                OrderStatus = "Processing", 
                OrderDate = DateTime.Now,
                ShippingAddress = $"{user.Address}, {user.City}, {user.Country}",
                OrderItems = orderItems,
                Transactions = new List<Transaction>()
            };

            // Create transaction linked to this order
            var transaction = new Transaction
            {
                Amount = total,
                PaymentMethod = "Card",
                TransactionStatus = "Completed", 
                TransactionDate = DateTime.Now,
                Order = order
            };

            order.Transactions.Add(transaction);

            try
            {
                // Save order + transaction
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Clear shopping cart
                HttpContext.Session.Remove("Cart");

                // Redirect to ThankYou page with orderId
                return RedirectToAction("ThankYou", "Orders", new { orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                // Log error and redirect back to cart
                Console.WriteLine($"Σφάλμα στην ολοκλήρωση παραγγελίας: {ex.Message}");
                TempData["Error"] = "Υπήρξε σφάλμα κατά την ολοκλήρωση της παραγγελίας. Δοκίμασε ξανά.";
                return RedirectToAction("Cart", "Products");
            }
        }

        // Admin: view all orders
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View("AllOrders", orders);
        }

        // Admin: delete an order
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Transactions)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            // Restore product stock
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.Quantity += item.Quantity;
            }

            // Delete related entities first
            _context.OrderItems.RemoveRange(order.OrderItems);
            _context.Transactions.RemoveRange(order.Transactions);

            // Then delete the order itself
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order #{id} has been deleted.";
            return RedirectToAction(nameof(AllOrders));
        }

    }
}