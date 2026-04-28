using System.ComponentModel.DataAnnotations;

namespace Taller.Models;

public class Reparacion
{
    public int Id { get; set; }

    [Required]
    public string Patente { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "El kilometraje es obligatorio.")]
    [Range(0, 2000000, ErrorMessage = "Ingrese un kilometraje válido.")]
    public int Kilometraje { get; set; }

    [Required(ErrorMessage = "Debe ingresar el detalle del trabajo.")]
    public string Detalle { get; set; } = string.Empty;

    public Vehiculo? Vehiculo { get; set; }
}