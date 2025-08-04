namespace Enfermeria_app.ViewModels // <-- CAMBIA "TuProyecto"
{
    public class HoraViewModel
    {
        public int IdHorario { get; set; }
        public string Hora { get; set; } // Ej: "07:00 AM"
        public int CantidadCitasProgramadas { get; set; }

        // --- NUEVA PROPIEDAD AÑADIDA ---
        public string Estado { get; set; } // "Activo" o "Cancelado"
    }
}