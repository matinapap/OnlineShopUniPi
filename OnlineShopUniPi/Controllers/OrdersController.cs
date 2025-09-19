using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Helpers;
using OnlineShopUniPi.Models;

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
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Get all orders that include at least one product owned by this user
            var ordersQuery = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.OrderItems.Any(oi => oi.Product.UserId == userId));

            // Filter pending vs history
            if (filter == "Pending")
            {
                ordersQuery = ordersQuery.Where(o => o.OrderStatus == "Processing");
            }
            else if (filter == "History")
            {
                ordersQuery = ordersQuery.Where(o => o.OrderStatus != "Processing");
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Filter = filter;

            return View(orders);
        }

        // Purchases made by the logged-in user
        [Authorize]
        public async Task<IActionResult> MyPurchases(string filter = "Pending")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Query the user's orders, excluding their own products
            var purchasesQuery = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId)
                .Where(o => o.OrderItems.Any(oi => oi.Product.UserId != userId));

            // Filter by Pending or History based on order status
            if (filter == "Pending")
                purchasesQuery = purchasesQuery.Where(o => o.OrderStatus == "Processing");
            else if (filter == "History")
                purchasesQuery = purchasesQuery.Where(o => o.OrderStatus != "Processing");

            // Execute query and order by most recent first
            var purchases = await purchasesQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Store the selected filter in ViewBag for use in the view
            ViewBag.Filter = filter;

            return View(purchases);
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
                string status = statusProp.GetString();

                // Find order including OrderItems and Product
                var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                // If status is being changed to Cancelled, return stock quantities
                if (status == "Cancelled" && order.OrderStatus != "Cancelled")
                {
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.Quantity += item.Quantity; // restore stock
                        }
                    }
                }

                // Update order status
                order.OrderStatus = status;
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
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            // Optional: restore product stock
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.Quantity += item.Quantity;
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order #{id} has been deleted.";
            return RedirectToAction(nameof(AllOrders));
        }

    }
}