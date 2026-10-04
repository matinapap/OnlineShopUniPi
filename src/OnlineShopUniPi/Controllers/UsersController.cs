using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;


namespace OnlineShopUniPi.Controllers
{
    public class UsersController : Controller
    {
        private readonly OnlineStoreDBContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UsersController(OnlineStoreDBContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // GET: Users
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string? searchId, string? searchUsername)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchId) && int.TryParse(searchId, out int id))
            {
                users = users.Where(u => u.UserId == id);
            }

            if (!string.IsNullOrEmpty(searchUsername))
            {
                users = users.Where(u => u.Username.Contains(searchUsername));
            }

            return View(await users.ToListAsync());
        }

        public IActionResult LoginSignup()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string lemail, string lpassword)
        {

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == lemail);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Δεν βρέθηκε λογαριασμός με αυτό το email.");
                ViewData["Form"] = "login";
                ViewData["LoginEmail"] = lemail; 
                return View("LoginSignup");
            }

            var hashedInput = HashPassword(lpassword);
            if (hashedInput != user.PasswordHash)
            {
                ModelState.AddModelError(string.Empty, "Λάθος κωδικός.");
                ViewData["Form"] = "login";
                ViewData["LoginEmail"] = lemail; // preserves email
                return View("LoginSignup");
            }

            // If the check passes, proceed with login
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FirstName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Details", new { id = user.UserId });
        }


        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: Users/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (!User.IsInRole("Admin") && currentUserId != id)
                return Forbid();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ChangePassword(int userId, string NewPassword, string ConfirmPassword)
        {
            if (NewPassword != ConfirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return RedirectToAction("Details", new { id = userId });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.PasswordHash = HashPassword(NewPassword);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Password updated successfully.";
            return RedirectToAction("Details", new { id = userId });
        }


        // GET: Users/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            bool hasErrors = false;

            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                ModelState.AddModelError("Email", "Το email χρησιμοποιείται ήδη.");
                hasErrors = true;
            }
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Το username χρησιμοποιείται ήδη.");
                hasErrors = true;
            }

            if (!ModelState.IsValid || hasErrors)
            {
                ViewData["Form"] = "signup";
                return View("LoginSignup", user);
            }

            // Hash password & save
            user.PasswordHash = HashPassword(user.PasswordHash);
            user.RegistrationDate = DateTime.UtcNow;

            _context.Add(user);
            await _context.SaveChangesAsync();

            // Login
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FirstName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Details", new { id = user.UserId });
        }


        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatedByAdmin(
      [Bind("UserId,FirstName,LastName,Email,Username,PasswordHash,PhoneNumber,ProfilePictureUrl,Address,City,Country,Role,RegistrationDate")] User user,
      string ConfirmPassword)
        {
            if (user.PasswordHash != ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Οι κωδικοί δεν ταιριάζουν.");
            }

            var existingEmailUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            var existingUsernameUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);

            if (existingEmailUser != null)
            {
                ModelState.AddModelError("Email", "Το email χρησιμοποιείται ήδη.");
            }

            if (existingUsernameUser != null)
            {
                ModelState.AddModelError("Username", "Το username χρησιμοποιείται ήδη.");
            }

            if (!ModelState.IsValid)
            {
                return View("Create", user);
            }

            user.PasswordHash = HashPassword(user.PasswordHash);
            _context.Add(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // GET: Users/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null)
                return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user is null)
                return NotFound();

            // Get current logged-in user ID
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Allow access only if Admin or owner of the profile
            var canEdit = User.IsInRole("Admin") || currentUserId == user.UserId;
            if (!canEdit)
                return Forbid();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, IFormFile? ProfilePictureFile)  // nullable
        {
            var userFromDb = await _context.Users.FindAsync(id);
            if (userFromDb == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (!User.IsInRole("Admin") && currentUserId != id)
                return Forbid();

            ModelState.Remove("ProfilePictureFile");

            var success = await TryUpdateModelAsync(userFromDb, "",
                u => u.FirstName,
                u => u.LastName,
                u => u.Username,
                u => u.Email,
                u => u.PhoneNumber,
                u => u.Address,
                u => u.City,
                u => u.Country,
                u => u.Role);

            if (User.IsInRole("Admin"))
            {
                await TryUpdateModelAsync(userFromDb, "", u => u.Role);
            }

            if (!success)
            {
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        Console.WriteLine($"ModelState error in '{entry.Key}': {error.ErrorMessage}");
                    }
                }
                return View(userFromDb);
            }

            if (ProfilePictureFile != null && ProfilePictureFile.Length > 0)
            {
                var fileExt = Path.GetExtension(ProfilePictureFile.FileName);
                var fileName = $"{userFromDb.Username}{fileExt}";
                var folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var savePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ProfilePictureFile.CopyToAsync(stream);
                }

                userFromDb.ProfilePictureUrl = $"/users/{fileName}";
            }
            else
            {
                var profilePictureUrlFromForm = Request.Form["ProfilePictureUrl"].ToString();
                if (!string.IsNullOrWhiteSpace(profilePictureUrlFromForm))
                {
                    userFromDb.ProfilePictureUrl = profilePictureUrlFromForm;
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = userFromDb.UserId });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(e => e.UserId == id))
                    return NotFound();
                throw;
            }
        }

        // GET: Users/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (!User.IsInRole("Admin") && currentUserId != id)
                return Forbid();

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var user = await _context.Users
                .Include(u => u.Products)
                    .ThenInclude(p => p.ProductImages)
                .Include(u => u.Products)
                    .ThenInclude(p => p.Favorites)
                .Include(u => u.Products)
                    .ThenInclude(p => p.OrderItems)
                .Include(u => u.Favorites)
                .Include(u => u.Orders)
                    .ThenInclude(o => o.OrderItems)
                .Include(u => u.Orders)
                    .ThenInclude(o => o.Transactions)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (!User.IsInRole("Admin") && currentUserId != id)
                return Forbid();

            _context.Favorites.RemoveRange(user.Favorites);

            foreach (var order in user.Orders)
            {
                _context.OrderItems.RemoveRange(order.OrderItems);
                _context.Transactions.RemoveRange(order.Transactions);
            }
            _context.Orders.RemoveRange(user.Orders);

            foreach (var product in user.Products)
            {
                _context.Favorites.RemoveRange(product.Favorites);
                _context.OrderItems.RemoveRange(product.OrderItems);
                _context.ProductImages.RemoveRange(product.ProductImages);
            }
            _context.Products.RemoveRange(user.Products);

            _context.Users.Remove(user);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}