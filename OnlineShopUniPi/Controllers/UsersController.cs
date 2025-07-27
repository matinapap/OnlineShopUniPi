using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;


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
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
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
                new Claim(ClaimTypes.Email, user.Email)
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

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // Κάνε hash το νέο password πριν το αποθηκεύσεις
            user.PasswordHash = HashPassword(NewPassword);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Password updated successfully.";
            return RedirectToAction("Details", new { id = userId });
        }


        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,FirstName,LastName,Email,Username,PasswordHash,PhoneNumber,ProfilePictureUrl,Address,City,Country,Role,RegistrationDate")] User user)
        {

            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Το email χρησιμοποιείται ήδη.");
                    ViewData["Form"] = "signup";
                    return View("LoginSignup", user); // Early return, do not proceed with saving
                }

                //Hashing the password
                user.PasswordHash = HashPassword(user.PasswordHash);

                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new { id = user.UserId });
            }

            ViewData["Form"] = "signup";
            return View("LoginSignup", user);
        }




        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormFile? ProfilePictureFile)  // nullable
        {
            var userFromDb = await _context.Users.FindAsync(id);
            if (userFromDb == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (currentUserId != id)
                return Forbid();

            // Αφαιρούμε το ModelState error αν υπάρχει για το ProfilePictureFile
            ModelState.Remove("ProfilePictureFile");

            // Προσπάθησε να ενημερώσεις τα πεδία από το form (εκτός της εικόνας)
            var success = await TryUpdateModelAsync(userFromDb, "",
                u => u.FirstName,
                u => u.LastName,
                u => u.Username,
                u => u.Email,
                u => u.PhoneNumber,
                u => u.Address,
                u => u.City,
                u => u.Country);

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

            // Ανέβασε νέα εικόνα αν υπάρχει
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
                // Αν δεν ανέβηκε νέα εικόνα, πάρε το URL από το hidden input
                var profilePictureUrlFromForm = Request.Form["ProfilePictureUrl"].ToString();
                if (!string.IsNullOrWhiteSpace(profilePictureUrlFromForm))
                {
                    userFromDb.ProfilePictureUrl = profilePictureUrlFromForm;
                }
                // Αν είναι κενό, κρατάμε την προηγούμενη τιμή
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

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }
    }
}
