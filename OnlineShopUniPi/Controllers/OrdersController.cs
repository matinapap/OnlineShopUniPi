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

        [HttpGet]
        public IActionResult Checkout()
        {
            return View();
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

            var cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            if (!cart.Any())
            {
                TempData["Error"] = "Το καλάθι είναι άδειο.";
                return RedirectToAction("Cart", "Product");
            }

            var products = await _context.Products
                .Where(p => cart.Contains(p.ProductId))
                .ToListAsync();

            var total = products.Sum(p => p.Price);

            var order = new Order
            {
                UserId = userId,
                TotalPrice = total,
                OrderStatus = "Σε επεξεργασία",
                OrderDate = DateTime.Now,
                ShippingAddress = $"{user.Address}, {user.City}, {user.Country}",
                OrderItems = new List<OrderItem>(),
                Transactions = new List<Transaction>()
            };

            foreach (var product in products)
            {
                order.OrderItems.Add(new OrderItem
                {
                    ProductId = product.ProductId,
                    Quantity = 1,
                    Price = product.Price
                });
            }

            var transaction = new Transaction
            {
                Amount = total,
                PaymentMethod = paymentMethod,
                TransactionStatus = "Ολοκληρώθηκε",
                TransactionDate = DateTime.Now
            };

            order.Transactions.Add(transaction);

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("Cart");

            return RedirectToAction("ThankYou");
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
