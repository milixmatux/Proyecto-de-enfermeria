using Enfermeria_app.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BCrypt.Net;

namespace Enfermeria_app.Controllers
{
    public class CuentaController : Controller
    {
        private readonly EnfermeriaContext _context;

        // Constructor
        public CuentaController(EnfermeriaContext context)
        {
            _context = context;

            // ⚠️ Método temporal: ejecutar una sola vez para convertir contraseñas existentes
            //HashearPasswordsExistentes();
        }

        // =========================
        // Método temporal para convertir contraseñas existentes a hash
        private void HashearPasswordsExistentes()
        {
            var usuarios = _context.EnfPersonas.ToList();

            foreach (var user in usuarios)
            {
                // Solo actualizar si la contraseña no está hasheada
                if (!user.Password.StartsWith("$2a$"))
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
                }
            }

            _context.SaveChanges();
            Console.WriteLine("Contraseñas existentes convertidas a hash BCrypt correctamente.");
        }

        // =========================
        // ACCESO DENEGADO
        public IActionResult AccesoDenegado() => View();

        // =========================
        // LOGIN
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

            // Buscar el usuario por nombre
            var user = _context.EnfPersonas.FirstOrDefault(u => u.Usuario == model.Usuario);

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Contraseña, user.Password))
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View(model);
            }

            if (!user.Activo)
            {
                ViewBag.Error = "Usuario desactivado. Contacte al administrador.";
                return View(model);
            }

            // Guardar tipo de usuario en sesión
            HttpContext.Session.SetString("Usuario", user.Usuario);
            HttpContext.Session.SetString("TipoUsuario", user.Tipo);

            // Crear Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Usuario),
                new Claim("TipoUsuario", user.Tipo),
                new Claim("Nombre", user.Nombre)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

            return RedirectToAction("Inicio", "Inicio");
        }

        // =========================
        // REGISTRO
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

            // Validaciones
            if (_context.EnfPersonas.Any(u => u.Cedula == model.Cedula))
            {
                ViewBag.Error = "La cédula ya está registrada.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(u => u.Nombre == model.Nombre))
            {
                ViewBag.Error = "El nombre ya está registrado.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(u => u.Email == model.Email))
            {
                ViewBag.Error = "El correo ya está registrado.";
                return View(model);
            }

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
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
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
        // CERRAR SESIÓN
        [HttpPost]
        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Cuenta");
        }
    }
}
