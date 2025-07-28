using Microsoft.AspNetCore.Mvc;
using Enfermeria_app.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Enfermeria_app.Controllers
{
    public class CuentaController : Controller
    {
        private readonly EnfermeriaContext _context;

        public CuentaController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ========== LOGIN ==========
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.EnfPersonas
                .FirstOrDefault(u => u.Usuario == model.Usuario && u.Password == model.Contraseña);

            if (user == null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View(model);
            }

            // Guardar tipo de usuario en sesión (opcional, si querés usarlo además de Claims)
            HttpContext.Session.SetString("TipoUsuario", user.Tipo);

            // Crear los Claims (información del usuario para la cookie)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Usuario),
                new Claim("TipoUsuario", user.Tipo),
                new Claim("Nombre", user.Nombre)
            };
            
            // Crear identidad y principal
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Crear propiedades de autenticación
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // "Recordarme"
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            // Iniciar sesión
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

            return RedirectToAction("Inicio", "Inicio");
        }

        // ========== REGISTRO ==========
        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

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

            var nuevaPersona = new EnfPersona
            {
                Cedula = model.Cedula,
                Nombre = model.Nombre,
                Telefono = model.Telefono,
                Email = model.Email,
                Usuario = model.Usuario,
                Password = model.Password, // ⚠️ Para producción deberías usar hashing
                Departamento = model.Departamento,
                Tipo = model.Tipo,
                Seccion = model.Seccion,
                FechaNacimiento = model.FechaNacimiento,
                Sexo = model.Sexo
            };

            _context.EnfPersonas.Add(nuevaPersona);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", "Cuenta");
        }

        // ========== CERRAR SESIÓN ==========
        [HttpPost]
        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Cuenta");
        }
    }
}
