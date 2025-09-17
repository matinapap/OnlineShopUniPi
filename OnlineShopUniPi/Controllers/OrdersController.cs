using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public IActionResult ThankYou()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> MyOrders(string filter = "Pending")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var ordersQuery = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId);

            if (filter == "Pending")
            {
                // Φέρνουμε μόνο τις παραγγελίες που είναι "Processing"
                ordersQuery = ordersQuery.Where(o => o.OrderStatus == "Processing");
            }
            else if (filter == "History")
            {
                // Φέρνουμε όλες τις υπόλοιπες παραγγελίες (Completed, Canceled)
                ordersQuery = ordersQuery.Where(o => o.OrderStatus != "Processing");
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Filter = filter;

            return View(orders);
        }

        [Authorize]
        public async Task<IActionResult> MyPurchases(string filter = "Pending")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var purchasesQuery = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId) // αγορές του χρήστη
                .Where(o => o.OrderItems.Any(oi => oi.Product.UserId != userId)); // μόνο προϊόντα άλλων χρηστών

            if (filter == "Pending")
            {
                purchasesQuery = purchasesQuery.Where(o => o.OrderStatus == "Processing");
            }
            else if (filter == "History")
            {
                purchasesQuery = purchasesQuery.Where(o => o.OrderStatus != "Processing");
            }

            var purchases = await purchasesQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Filter = filter;

            return View(purchases);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
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

                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                order.OrderStatus = status;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(string paymentMethod)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users.FindAsync(userId);

            // Πλέον χρησιμοποιούμε Dictionary<int,int> για να κρατάει ποσότητες
            var cart = HttpContext.Session.GetObjectFromJson<Dictionary<int, int>>("Cart")
                       ?? new Dictionary<int, int>();

            if (!cart.Any())
            {
                TempData["Error"] = "Το καλάθι είναι άδειο.";
                return RedirectToAction("Cart", "Products");
            }

            var productIds = cart.Keys.ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                TempData["Error"] = "Κάποια προϊόντα δεν είναι διαθέσιμα.";
                return RedirectToAction("Cart", "Products");
            }

            decimal total = 0;
            var orderItems = new List<OrderItem>();

            foreach (var product in products)
            {
                var quantity = cart[product.ProductId];

                if (quantity > product.Quantity)
                {
                    TempData["Error"] = $"Δεν υπάρχει αρκετό από το προϊόν '{product.Title}'.";
                    return RedirectToAction("Cart", "Products");
                }

                // Μείωση αποθέματος
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

            var order = new Order
            {
                UserId = userId,
                TotalPrice = total,
                OrderStatus = "Processing", // Αλλαγή από "Σε επεξεργασία" σε "Processing"
                OrderDate = DateTime.Now,
                ShippingAddress = $"{user.Address}, {user.City}, {user.Country}",
                OrderItems = orderItems,
                Transactions = new List<Transaction>()
            };

            var transaction = new Transaction
            {
                Amount = total,
                PaymentMethod = paymentMethod,
                TransactionStatus = "Completed", // Αλλαγή από "Ολοκληρώθηκε" σε "Completed"
                TransactionDate = DateTime.Now,
                Order = order
            };

            order.Transactions.Add(transaction);

            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove("Cart");

                return RedirectToAction("ThankYou");
            }
            catch (Exception ex)
            {
                // Logging
                Console.WriteLine($"Σφάλμα στην ολοκλήρωση παραγγελίας: {ex.Message}");
                TempData["Error"] = "Υπήρξε σφάλμα κατά την ολοκλήρωση της παραγγελίας. Δοκίμασε ξανά.";
                return RedirectToAction("Cart", "Products");
            }
        }



        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var onlineStoreDBContext = _context.Orders.Include(o => o.User);
            return View(await onlineStoreDBContext.ToListAsync());
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(m => m.OrderId == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Orders/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId");
            return View();
        }

        // POST: Orders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OrderId,UserId,TotalPrice,OrderStatus,OrderDate,ShippingAddress")] Order order)
        {
            if (ModelState.IsValid)
            {
                _context.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", order.UserId);
            return View(order);
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", order.UserId);
            return View(order);
        }

        // POST: Orders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrderId,UserId,TotalPrice,OrderStatus,OrderDate,ShippingAddress")] Order order)
        {
            if (id != order.OrderId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.OrderId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", order.UserId);
            return View(order);
        }

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(m => m.OrderId == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                _context.Orders.Remove(order);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderId == id);
        }
    }
}
