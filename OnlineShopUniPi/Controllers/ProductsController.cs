using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Models;

namespace OnlineShopUniPi.Controllers
{
    public class ProductsController : Controller
    {
        private readonly OnlineStoreDBContext _context;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(OnlineStoreDBContext context, ILogger<ProductsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.User)
                    .ToListAsync();

                return View(products ?? new List<Product>()); // Πάντα μη-null
            }
            catch (Exception ex)
            {
                // Logging εδώ
                return View(new List<Product>());
            }
        }

        [Authorize]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdValue))
                    return Unauthorized();

                if (!int.TryParse(userIdValue, out int userId))
                    return Unauthorized();

                var products = await _context.Products
                    .Include(p => p.User)
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                return View("ProductList", products);
            }
            catch (Exception ex)
            {
                // Κάνε logging εδώ ή debugging για να δεις τι σφάλμα προέκυψε
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }




        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Products == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.User)             
                .Include(p => p.ProductImages)    
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            // Παίρνουμε το UserId από τα claims του χρήστη
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Αν δεν υπάρχει UserId, επιστρέφουμε Unauthorized
            if (string.IsNullOrEmpty(userIdValue))
            {
                Console.WriteLine("1. DEN DOULEYEIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII!");

                return Unauthorized();
            }

            // Βάζουμε το UserId στο ViewData για να το έχουμε στο View αν χρειαστεί
            ViewData["UserId"] = userIdValue;

            return View();
        }


        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,Title,Description,Category,Price,Condition")] Product product)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
            {
                return Unauthorized();
            }

            // Instead of loading the entire user, just set the foreign key
            product.UserId = userId;
            product.CreatedAt = DateTime.UtcNow;

            // Clear the ModelState error for User since we're setting UserId directly
            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save product");
                ModelState.AddModelError("", "Failed to save product. Please try again.");
                return View(product);
            }
        }


        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", product.UserId);
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,UserId,Title,Description,Category,Price,Condition,CreatedAt")] Product product)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId))
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
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", product.UserId);
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }
    }
}
