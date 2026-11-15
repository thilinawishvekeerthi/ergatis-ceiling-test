using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using UserRegistration.Web.Data;
using UserRegistration.Web.Models;
using UserRegistration.Web.ViewModels;

namespace UserRegistration.Web.Controllers;

public class UsersController(AppDbContext db, IPasswordHasher<User> hasher) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = await db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAtUtc).ToListAsync();
        return View(users);
    }

    // Modal body: the register form, loaded into the modal by fetch().
    [HttpGet]
    public IActionResult Register() => PartialView("_RegisterForm", new RegisterViewModel());

    // Success returns 200 + JSON; validation errors return 422 + the form re-rendered so the modal can swap it in.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var email = model.Email.Trim().ToLowerInvariant();

        if (ModelState.IsValid && await db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
        }

        if (!ModelState.IsValid)
        {
            return InvalidForm(model);
        }

        var user = new User { FullName = model.FullName.Trim(), Email = email, PasswordHash = "", CreatedAtUtc = DateTime.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, model.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests with the same email can pass the check above together; the unique index decides.
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            return InvalidForm(model);
        }

        return Json(new { ok = true, message = $"Welcome, {user.FullName}! Your account was created." });
    }

    // Modal body: delete confirmation.
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? NotFound() : PartialView("_DeleteConfirm", user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
        return Json(new { ok = true, message = "User deleted." });
    }

    private IActionResult InvalidForm(RegisterViewModel model)
    {
        // Never send a password back to the browser.
        model.Password = model.ConfirmPassword = "";
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView("_RegisterForm", model);
    }
}
