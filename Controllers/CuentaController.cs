using Enfermeria_app.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Enfermeria_app.Controllers
{
    [AllowAnonymous]
    public class CuentaController : Controller
    {
        private readonly EnfermeriaContext _context;

        public CuentaController(EnfermeriaContext context)
        {
            _context = context;
        }

        // =========================
        // LOGIN
        // =========================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.EnfPersonas.FirstOrDefault(u => u.Usuario == model.Usuario);
            if (user == null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View(model);
            }

            bool passwordValida =
                (user.Password == model.Contraseña) ||
                BCrypt.Net.BCrypt.Verify(model.Contraseña, user.Password);

            if (!passwordValida)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View(model);
            }

            if (!user.Activo)
            {
                ViewBag.Error = "Usuario desactivado.";
                return View(model);
            }

            // GUARDA SESIÓN
            HttpContext.Session.SetString("Usuario", user.Usuario);
            HttpContext.Session.SetString("NombreCompleto", user.Nombre ?? user.Usuario);
            HttpContext.Session.SetString("TipoUsuario", user.Tipo);
            HttpContext.Session.SetString("Departamento", user.Departamento ?? "");

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Usuario),
        new Claim("TipoUsuario", user.Tipo)
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

            if (model.Contraseña == "agro2025")
                return RedirectToAction("CambiarPassword", new { usuario = user.Usuario });

            return RedirectToAction("Inicio", "Inicio");
        }

        // =========================
        // REGISTRO
        // =========================
        [HttpGet]
        public IActionResult Registro() => View();

        [HttpPost]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (_context.EnfPersonas.Any(u => u.Usuario == model.Usuario))
            {
                ViewBag.Error = "El usuario ya existe.";
                return View(model);
            }

            // Guardar contraseña en texto plano si es agro2025, de lo contrario con hash
            string password = model.Password == "agro2025"
                ? model.Password
                : BCrypt.Net.BCrypt.HashPassword(model.Password);

            var nuevaPersona = new EnfPersona
            {
                Cedula = model.Cedula,
                Nombre = model.Nombre,
                Telefono = model.Telefono,
                Email = model.Email,
                Usuario = model.Usuario,
                Password = password,
                Departamento = model.Departamento,
                Tipo = model.Tipo,
                Seccion = model.Seccion,
                FechaNacimiento = model.FechaNacimiento,
                Sexo = model.Sexo,
                Activo = true
            };

            _context.EnfPersonas.Add(nuevaPersona);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", "Cuenta");
        }

        // =========================
        // CAMBIAR PASSWORD
        // =========================
        [Authorize]
        [HttpGet]
        public IActionResult CambiarPassword(string? usuario = null)
        {
            usuario ??= User?.Identity?.Name;
            if (usuario == null) return RedirectToAction("Login");

            ViewBag.Usuario = usuario;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarPassword(string usuario, string passwordActual, string nuevaPassword, string confirmarPassword)
        {
            var user = _context.EnfPersonas.FirstOrDefault(u => u.Usuario == usuario);
            if (user == null)
            {
                ViewBag.Error = "Usuario no encontrado.";
                return View();
            }

            // Validar la contraseña actual
            bool passwordValida =
                (user.Password == passwordActual) ||
                BCrypt.Net.BCrypt.Verify(passwordActual, user.Password);

            if (!passwordValida)
            {
                ViewBag.Error = "La contraseña actual es incorrecta.";
                ViewBag.Usuario = usuario;
                return View();
            }

            // Validar que cumpla los requisitos
            if (!EsPasswordSegura(nuevaPassword))
            {
                ViewBag.Error = "La nueva contraseña no cumple con los requisitos.";
                ViewBag.Usuario = usuario;
                return View();
            }

            if (nuevaPassword != confirmarPassword)
            {
                ViewBag.Error = "Las contraseñas no coinciden.";
                ViewBag.Usuario = usuario;
                return View();
            }

            // Guardar con hash
            user.Password = BCrypt.Net.BCrypt.HashPassword(nuevaPassword);
            _context.EnfPersonas.Update(user);
            await _context.SaveChangesAsync();

            // 🔁 Redirigir al inicio después del cambio exitoso
            TempData["Mensaje"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Inicio", "Inicio");
        }

        private bool EsPasswordSegura(string password)
        {
            // Al menos 8 caracteres, una mayúscula, una minúscula, un número y un símbolo
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$");
            return regex.IsMatch(password);
        }

        // =========================
        // CERRAR SESIÓN
        // =========================
        [HttpPost]
        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Cuenta");
        }
    }
}
