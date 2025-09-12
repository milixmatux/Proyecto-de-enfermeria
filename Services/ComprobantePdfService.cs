using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enfermeria_app.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Enfermeria_app.Services
{
    public class ComprobantePdfService
    {
        public ComprobantePdfService() { }

        // 📄 PDF de una sola cita
        public byte[] ComprobanteCita(EnfCita cita)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .PaddingBottom(10)
                        .Text($"Comprobante de cita #{cita.Id}")
                        .SemiBold().FontSize(16).AlignCenter();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                        });

                        table.Cell().Element(CellStyle).Text("Estudiante").Bold();
                        table.Cell().Element(CellStyle).Text(cita.IdPersonaNavigation?.Nombre ?? "-");

                        table.Cell().Element(CellStyle).Text("Sección").Bold();
                        table.Cell().Element(CellStyle).Text(cita.IdPersonaNavigation?.Seccion ?? "-");

                        table.Cell().Element(CellStyle).Text("Fecha").Bold();
                        table.Cell().Element(CellStyle).Text(cita.IdHorarioNavigation?.Fecha.ToString("dd/MM/yyyy") ?? "-");

                        table.Cell().Element(CellStyle).Text("Hora").Bold();
                        table.Cell().Element(CellStyle).Text(cita.IdHorarioNavigation?.Hora.ToString("HH\\:mm") ?? "-");

                        table.Cell().Element(CellStyle).Text("Llegada").Bold();
                        table.Cell().Element(CellStyle).Text(cita.HoraLlegada?.ToString("HH\\:mm") ?? "-");

                        table.Cell().Element(CellStyle).Text("Salida").Bold();
                        table.Cell().Element(CellStyle).Text(cita.HoraSalida?.ToString("HH\\:mm") ?? "-");

                        table.Cell().Element(CellStyle).Text("Observaciones").Bold();
                        var msg = !string.IsNullOrWhiteSpace(cita.MensajeSalida)
                            ? cita.MensajeSalida
                            : (!string.IsNullOrWhiteSpace(cita.MensajeLlegada) ? cita.MensajeLlegada : "-");
                        table.Cell().Element(CellStyle).Text(msg);
                    });
                });
            });

            using var stream = new MemoryStream();
            doc.GeneratePdf(stream);
            return stream.ToArray();
        }

        // 📄 PDF con rango de citas
        public byte[] ComprobantesRango(List<EnfCita> citas, DateOnly inicio, DateOnly fin)
        {
            if (!citas.Any())
                throw new InvalidOperationException("No hay citas registradas en el rango proporcionado.");

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .PaddingBottom(10)
                        .Text($"Comprobantes de Citas\nDel {inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}")
                        .SemiBold().FontSize(16).AlignCenter();

                    page.Content().Element(BuildTable(citas));
                });
            });

            using var stream = new MemoryStream();
            doc.GeneratePdf(stream);
            return stream.ToArray();
        }

        // Tabla general
        private Action<IContainer> BuildTable(List<EnfCita> citas)
        {
            return container =>
            {
                container.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Estudiante").Bold();
                        header.Cell().Element(CellStyle).Text("Sección").Bold();
                        header.Cell().Element(CellStyle).Text("Fecha").Bold();
                        header.Cell().Element(CellStyle).Text("Llegada").Bold();
                        header.Cell().Element(CellStyle).Text("Salida").Bold();
                        header.Cell().Element(CellStyle).Text("Observaciones").Bold();
                    });

                    foreach (var c in citas)
                    {
                        table.Cell().Element(CellStyle).Text(c.IdPersonaNavigation?.Nombre ?? "-");
                        table.Cell().Element(CellStyle).Text(c.IdPersonaNavigation?.Seccion ?? "-");
                        table.Cell().Element(CellStyle).Text(c.IdHorarioNavigation?.Fecha.ToString("dd/MM/yyyy") ?? "-");
                        table.Cell().Element(CellStyle).Text(c.HoraLlegada?.ToString("HH\\:mm") ?? "-");
                        table.Cell().Element(CellStyle).Text(c.HoraSalida?.ToString("HH\\:mm") ?? "-");

                        var msg = !string.IsNullOrWhiteSpace(c.MensajeSalida)
                            ? c.MensajeSalida
                            : (!string.IsNullOrWhiteSpace(c.MensajeLlegada) ? c.MensajeLlegada : "-");

                        table.Cell().Element(CellStyle).Text(msg);
                    }
                });
            };
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(5);
        }
    }
}
