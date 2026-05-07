# AutoSys - Instalador para cliente (Windows 8/10/11)

Este flujo genera un instalador `.exe` para instalar AutoSys con un click.

## 1) Requisitos en tu PC de armado

- .NET SDK 6 instalado (para `dotnet publish`)
- Inno Setup 6 instalado (para compilar el setup final)
- WebView2 Runtime instalado en la PC destino si Windows no lo trae ya.

## 2) Generar instalador completo

Desde la raiz del proyecto, el instalador compatible con Windows 8 y Windows 10 se genera con:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 1.0.7
```

Esto publicará la variante `net6.0-windows` en `dist\publish` y, si Inno Setup está instalado en la máquina de build, compilará automáticamente el instalador `.exe` en `dist\output`.

Salida esperada:

- Publicacion app: `dist\publish\`
- Instalador final: `dist\output\AutoSys-Setup-1.0.7.exe`

## 3) Instalacion en la PC del taller

1. Copiar `AutoSys-Setup-<version>.exe` a la PC del cliente.
2. Ejecutar como administrador.
3. Seguir asistente (siguiente, instalar, finalizar).
4. Abrir AutoSys desde acceso directo.

## 4) Notas operativas importantes

- La base de datos se guarda en:
  `Documentos\AutoSys\taller.db`
- El log de arranque queda en:
  `Documentos\AutoSys\startup.log`
- Si el cliente reinstala AutoSys, los datos se conservan porque no viven dentro de `Program Files`.

## 5) Compatibilidad

- La app se publica como self-contained en .NET 6 x64 para Windows 8.1 y Windows 10.
- La app levanta servidor local y abre la interfaz integrada.
- El instalador admite Windows 8 en adelante (`MinVersion=6.2`).
