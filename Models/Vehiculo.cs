using System.ComponentModel.DataAnnotations;

namespace Taller.Models;

public class Vehiculo
{
    [Key]
    [Required(ErrorMessage = "La patente es obligatoria.")]
    [StringLength(10)]
    public string Patente { get; set; } = string.Empty;
    public string PatenteFormateada
{
    get
    {
        if (string.IsNullOrWhiteSpace(Patente)) return "";
        string p = Patente.Replace(" ", "").ToUpper();
        
        // Formato Nuevo: AA111AA -> AA 111 AA
        if (p.Length == 7)
            return $"{p.Substring(0, 2)} {p.Substring(2, 3)} {p.Substring(5, 2)}";
        
        // Formato Viejo: AAA111 -> AAA 111
        if (p.Length == 6)
            return $"{p.Substring(0, 3)} {p.Substring(3, 3)}";
            
        return p; // Por si hay algo raro
    }
}

    [Required(ErrorMessage = "La marca es obligatoria.")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    public string Modelo { get; set; } = string.Empty;

    public List<Reparacion> Reparaciones { get; set; } = new();
}