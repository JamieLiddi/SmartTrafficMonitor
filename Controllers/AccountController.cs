using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OtpNet;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Controllers
{
    public class AccountController : Controller
    {
        /* For simplicity, we have a single admin user defined in config. In a real app, you'd have a user database.
        */
        private readonly AuthSettings _auth;
        private readonly IAuditLogService _audit;

        public AccountController(IOptions<AuthSettings> auth, IAuditLogService audit)
        {
            _auth = auth.Value;
            _audit = audit;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/")
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password, string otp, string returnUrl = "/")
        {
            email = (email ?? "").Trim();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var isAdminEmail = string.Equals(email, _auth.AdminEmail, StringComparison.OrdinalIgnoreCase);

            if (!isAdminEmail || password != _auth.AdminPassword)
            {
                _audit.Log("login", $"email={email}", false, email, ip);
                ViewBag.Error = "Invalid login.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            if (string.IsNullOrWhiteSpace(_auth.AdminTotpSecretBase32))
            {
                _audit.Log("login", "admin totp secret missing in config", false, email, ip);
                ViewBag.Error = "Admin 2fa is not configured.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                _audit.Log("login", "missing otp", false, email, ip);
                ViewBag.Error = "Enter your 2fa code.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            byte[] secretBytes;
            try
            {
                secretBytes = Base32Encoding.ToBytes(_auth.AdminTotpSecretBase32);
            }
            catch
            {
                _audit.Log("login", "invalid totp secret format", false, email, ip);
                ViewBag.Error = "Admin 2fa config is invalid.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var totp = new Totp(secretBytes);
            var ok = totp.VerifyTotp(otp.Trim(), out _, new VerificationWindow(previous: 1, future: 1));

            if (!ok)
            {
                _audit.Log("login", "otp failed", false, email, ip);
                ViewBag.Error = "Invalid 2fa code.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                }
            ).GetAwaiter().GetResult();

            _audit.Log("login", "admin login success", true, email, ip);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var email = User?.Identity?.Name;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                .GetAwaiter().GetResult();

            _audit.Log("logout", "logout", true, email, ip);

            return RedirectToAction("Login", "Account");
        }
    }
}
