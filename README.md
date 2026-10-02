# Comerzia Printer Tester

Herramienta de diagnóstico, diseño y prueba de impresión de etiquetas térmicas mediante comandos **TSPL** sobre **TCP/IP** (Ethernet / Wi-Fi), diseñada para impresoras de etiquetas como la **PhiloPOS 320B** y compatibles (203 DPI).

---

## 🚀 Características

- **Previsualización en tiempo real:** Representación fidedigna de la etiqueta según dimensiones en milímetros y resolución térmica (203 DPI / 8 dots por mm).
- **Orientación inteligente:**
  - **Horizontal:** Disposición estándar para etiquetas con Ancho >= Alto (ej. 30x20 mm, 50x30 mm).
  - **Vertical (Giro 90° automático):** Para etiquetas con Alto > Ancho (ej. 31x57 mm, 38x62 mm), el contenido se orienta longitudinalmente y se rota 90° para aprovechar al máximo el largo del papel.
- **Códigos 1D y 2D:**
  - Código de barras 1D (**Code 128**, **EAN-13**, **Code 39**) 100% escaneable.
  - Código **QR** configurable mediante comandos nativos TSPL o renderizado por bitmap.
- **Logo térmico:** Carga de imagen con auto-recorte de márgenes en blanco, escalado y conversión optimizada a bitmap monocromático de 1 bit por pixel.
- **Formateo de Precio:** Precio en tamaño grande armónico con el logo, con el texto `"BS"` proporcional a 1/3 de la altura y alineado en la base. Si no se ingresa precio, se omite automáticamente.
- **Visor TSPL & Consola RAW:** Inspección del script TSPL generado y consola para envío directo de comandos personalizados.
- **Persistencia automática:** Guarda la última configuración usada en `appsettings.json`.

---

## 💻 Requisitos

- **Sistema Operativo:** Windows 10 / 11 (x64)
- **Para Desarrollar / Compilar:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Para Ejecutar:** Windows Desktop Runtime .NET 8.0 (si se compila dependiente del framework) o ninguno si se compila como autocontenido (`--self-contained true`).

---

## 🛠️ Compilación y Despliegue en Otra Computadora

### 1. Clonar el repositorio
```bash
git clone <URL_DEL_REPOSITORIO>
cd "Comerzia Printer Tester"
```

### 2. Compilar en modo Release
```powershell
dotnet build -c Release
```

### 3. Publicar para distribución en otra PC

#### Opción A: Publicación estándar (Requiere .NET 8 Desktop Runtime en la máquina destino)
```powershell
dotnet publish ComerziaPrinterTester/ComerziaPrinterTester.csproj -c Release -r win-x64 --self-contained false -o ./publish
```
*Genera una carpeta `publish/` ligera con el ejecutable `ComerziaPrinterTester.exe`.*

#### Opción B: Publicación autocontenida (Portable, NO requiere instalar .NET en la máquina destino)
```powershell
dotnet publish ComerziaPrinterTester/ComerziaPrinterTester.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish_standalone
```
*Genera un archivo único listo para copiar a cualquier PC con Windows.*

---

## 🖨️ Conexión con la Impresora

1. Verifique que la impresora térmica (ej. PhiloPOS 320B) esté encendida y conectada a la misma red local que la computadora.
2. Imprima la hoja de autodiagnóstico de la impresora (mantenga presionado el botón FEED al encenderla) para obtener su dirección IP actual.
3. Ingrese la **IP** y el **Puerto** (por defecto `9100`) en la aplicación.
4. Presione **"Probar Conexión"** para verificar el enlace TCP.
5. Seleccione o ingrese las medidas de la etiqueta y presione **"Imprimir Prueba"**.
