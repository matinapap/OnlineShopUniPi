using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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
                OrderStatus = "Σε επεξεργασία",
                OrderDate = DateTime.Now,
                ShippingAddress = $"{user.Address}, {user.City}, {user.Country}",
                OrderItems = orderItems,
                Transactions = new List<Transaction>()
            };

            var transaction = new Transaction
            {
                Amount = total,
                PaymentMethod = paymentMethod,
                TransactionStatus = "Ολοκληρώθηκε",
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
