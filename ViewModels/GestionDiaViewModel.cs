using System.ComponentModel;

namespace Enfermeria_app.ViewModels // <-- ¡RECUERDA CAMBIAR "TuProyecto" POR EL NOMBRE REAL DE TU PROYECTO!
{
    public class GestionDiaViewModel
    {
        [DisplayName("Fecha")]
        public DateTime FechaSeleccionada { get; set; }

        public int TotalCitasActivas { get; set; }

        public bool EsFinDeSemana { get; set; }

        public List<HoraViewModel> HorasDelDia { get; set; } = new List<HoraViewModel>();
    }
}