using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IWebHostEnvironment _env;

        public ProductsController(IWebHostEnvironment env, OnlineStoreDBContext context, ILogger<ProductsController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _env = env;
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Products
        public async Task<IActionResult> Index(int? productId)
        {
            try
            {
                IQueryable<Product> query = _context.Products
                    .Include(p => p.User);

                // Αν έχει δοθεί συγκεκριμένο ProductId, φιλτράρουμε
                if (productId.HasValue)
                {
                    query = query.Where(p => p.ProductId == productId.Value);
                }

                var products = await query.ToListAsync();
                return View(products);
            }
            catch (Exception ex)
            {
                // Logging ή αναφορά σφάλματος
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
                    .Include(p => p.ProductImages)
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
                return Unauthorized();
            

            // Βάζουμε το UserId στο ViewData για να το έχουμε στο View αν χρειαστεί
            ViewData["UserId"] = userIdValue;

            return View();
        }


        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,Title,Description,Gender,Category,Price,Condition")] Product product, IFormFile MainImage, List<IFormFile> AdditionalImages)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
            {
                return Unauthorized();
            }

            product.UserId = userId;
            product.CreatedAt = DateTime.UtcNow;

            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                // ΚΥΡΙΑ ΕΙΚΟΝΑ
                if (MainImage != null && MainImage.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(MainImage.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await MainImage.CopyToAsync(stream);
                    }

                    var mainProductImage = new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImageUrl = "/images/products/" + fileName,
                        IsMainImage = true
                    };

                    _context.ProductImages.Add(mainProductImage);
                }

                // ΔΕΥΤΕΡΕΥΟΥΣΕΣ ΕΙΚΟΝΕΣ
                if (AdditionalImages != null && AdditionalImages.Count > 0)
                {
                    foreach (var formFile in AdditionalImages)
                    {
                        if (formFile.Length > 0)
                        {
                            var fileName = Guid.NewGuid() + Path.GetExtension(formFile.FileName);
                            var filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await formFile.CopyToAsync(stream);
                            }

                            var productImage = new ProductImage
                            {
                                ProductId = product.ProductId,
                                ImageUrl = "/images/products/" + fileName,
                                IsMainImage = false
                            };

                            _context.ProductImages.Add(productImage);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("GetProducts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save product");
                ModelState.AddModelError("", "Απέτυχε η αποθήκευση του προϊόντος. Δοκιμάστε ξανά.");
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
        public async Task<IActionResult> Edit(
     int id,
     [Bind("ProductId,Title,Description,Gender,Category,Price,Condition")] Product product,
     IFormFile ?MainImage,
     List<IFormFile> ?AdditionalImages)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
            {
                return Unauthorized();
            }

            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                var fullProduct = await _context.Products
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.ProductId == product.ProductId);

                return View(fullProduct);
            }

            var existingProduct = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (existingProduct == null)
            {
                return NotFound();
            }

            // Ενημέρωση ιδιοτήτων
            existingProduct.Title = product.Title;
            existingProduct.Description = product.Description;
            existingProduct.Gender = product.Gender;
            existingProduct.Category = product.Category;
            existingProduct.Price = product.Price;
            existingProduct.Condition = product.Condition;
            existingProduct.UserId = userId;
            existingProduct.CreatedAt = DateTime.UtcNow;

            bool hasNewImages =
                (MainImage != null && MainImage.Length > 0) ||
                (AdditionalImages != null && AdditionalImages.Any(f => f.Length > 0));

            if (hasNewImages)
            {
                // Διαγραφή παλιών εικόνων
                foreach (var oldImage in existingProduct.ProductImages)
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, oldImage.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                _context.ProductImages.RemoveRange(existingProduct.ProductImages);

                var newImages = new List<ProductImage>();

                // Κύρια εικόνα
                if (MainImage != null && MainImage.Length > 0)
                {
                    var mainFileName = $"{Guid.NewGuid()}{Path.GetExtension(MainImage.FileName)}";
                    var mainFilePath = Path.Combine(_env.WebRootPath, "images", "products", mainFileName);

                    Directory.CreateDirectory(Path.GetDirectoryName(mainFilePath));

                    using (var stream = new FileStream(mainFilePath, FileMode.Create))
                    {
                        await MainImage.CopyToAsync(stream);
                    }

                    newImages.Add(new ProductImage
                    {
                        ImageUrl = $"/images/products/{mainFileName}",
                        IsMainImage = true
                    });
                }

                // Δευτερεύουσες
                if (AdditionalImages != null && AdditionalImages.Count > 0)
                {
                    foreach (var img in AdditionalImages)
                    {
                        if (img.Length > 0)
                        {
                            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(img.FileName)}";
                            var filePath = Path.Combine(_env.WebRootPath, "images", "products", fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await img.CopyToAsync(stream);
                            }

                            newImages.Add(new ProductImage
                            {
                                ImageUrl = $"/images/products/{fileName}",
                                IsMainImage = false
                            });
                        }
                    }
                }

                existingProduct.ProductImages = newImages;
            }
            // Αλλιώς κρατάμε τις παλιές εικόνες ως έχουν.

            _context.Update(existingProduct);
            await _context.SaveChangesAsync();

            return RedirectToAction("GetProducts");
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
            // Φόρτωσε το προϊόν μαζί με τις εικόνες του
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product != null)
            {
                // Διαγραφή των εικόνων από το δίσκο
                if (product.ProductImages != null)
                {
                    foreach (var image in product.ProductImages)
                    {
                        var imagePath = Path.Combine(_env.WebRootPath, image.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }

                    // Διαγραφή εγγραφών εικόνων από τη βάση
                    _context.ProductImages.RemoveRange(product.ProductImages);
                }

                // Διαγραφή του προϊόντος
                _context.Products.Remove(product);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("GetProducts");
        }


        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }
    }
}
