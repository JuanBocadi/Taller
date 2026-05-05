using System.ComponentModel.DataAnnotations;

namespace Taller.Models;

public class Vehiculo
{
    [Key]
    [Required(ErrorMessage = "La identificación es obligatoria.")]
    // Quitamos el Regex estricto de aquí para validarlo manualmente en el controlador
    [StringLength(20, ErrorMessage = "La identificación no puede superar los 20 caracteres.")]
    public string Patente { get; set; } = string.Empty;

    public bool EsMaquinaria { get; set; } = false; // <--- Nuevo campo

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "La marca debe tener entre 2 y 50 caracteres.")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "El modelo debe tener entre 1 y 50 caracteres.")]
    public string Modelo { get; set; } = string.Empty;

    public List<Reparacion> Reparaciones { get; set; } = new();

    public string PatenteFormateada
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Patente)) return "";
            string p = Patente.Replace(" ", "").ToUpper();
            
            // Si es maquinaria, no intentamos formatear como patente de auto
            if (EsMaquinaria) return p;

            if (p.Length == 7) return $"{p.Substring(0, 2)} {p.Substring(2, 3)} {p.Substring(5, 2)}";
            if (p.Length == 6) return $"{p.Substring(0, 3)} {p.Substring(3, 3)}";
            return p;
        }
    }
}