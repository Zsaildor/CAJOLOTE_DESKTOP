# 🥑 Cajolote Desktop

**Cajolote Desktop** es una aplicación de Punto de Venta (POS) moderna, rápida e intuitiva desarrollada para Windows utilizando **.NET 8** y **WPF**. Está diseñada para optimizar la gestión de ventas, inventario, cuentas fiadas/créditos de clientes, integración con lectores de código de barras e impresoras térmicas, así como sincronización en la nube con Firebase.

---

## 🚀 Características Principales

* **Punto de Venta (POS) Agil:** Búsqueda rápida por lector de código de barras o teclado, productos a granel/peso y cobro rápido en efectivo o cargado a crédito.
* **Control de Inventario:** Gestión completa de catálogo de productos y categorías con indicadores visuales y accesos rápidos.
* **Gestión de Créditos y Cuentas Fiadas (Notas):** Registro de deudas por cliente, abonos parciales/totales e impresión de recibos de pago.
* **Integración con Hardware:** Soporte para impresoras térmicas ESC/POS (puertos serie COM) y escáner de códigos de barras global.
* **Dashboard e Informes Analíticos:** Gráficas de rendimiento diario, productos más vendidos, ingresos por categoría y reportes exportables a PDF (QuestPDF).
* **Sincronización Cloud e Híbrida:** Funcionamiento 100% offline con SQLite local y sincronización automática en segundo plano con Firebase Firestore / Auth.

---

## 🛠️ Tecnologías Utilizadas

* **Framework:** .NET 8.0 (Windows Presentation Foundation - WPF)
* **Arquitectura:** MVVM (`CommunityToolkit.Mvvm`)
* **Base de Datos Local:** SQLite con Entity Framework Core 8.0.14
* **Diseño UI:** Material Design in XAML & LiveChartsCore (SkiaSharp)
* **Generación de Documentos:** QuestPDF & LottieSharp
* **Autenticación & Nube:** Firebase Auth & Firestore REST

---

## 📁 Estructura del Proyecto

```
Cajolote_desktop/
├── Assets/              # Recursos gráficos e íconos (Lottie, XAML)
├── Converters/          # Convertidores de datos para vistas WPF
├── Data/                # DbContext y Seeder de Entity Framework Core
├── documentation/       # Documentación técnica completa del proyecto
│   ├── ARCHITECTURE.md  # Arquitectura, patrones MVVM y flujo de datos
│   ├── DICCIONARIO_DATOS.md # Esquema detallado de tablas de SQLite
│   └── FUNCIONALIDADES.md  # Catálogo detallado de funciones por vista
├── Messages/            # Mensajes de comunicación desacoplada (WeakReferenceMessenger)
├── Migraciones/         # Migraciones de esquema de EF Core
├── Models/              # Entidades del dominio (Product, Sale, Note, etc.)
├── Repositories/        # Capa de repositorios y almacenamiento cifrado
├── Services/            # Lógica de negocio (Impresora, Sync, Firebase, Archivador)
├── ViewModels/          # Lógica de presentación MVVM
├── Views/               # Vistas e interfaces XAML
└── Cajolote.sln         # Solución principal de .NET
```

---

## 📚 Documentación

Para obtener información detallada sobre el diseño y funcionamiento interno del sistema, consulta los siguientes documentos en la carpeta [`documentation/`](./documentation):

1. 🏗️ [**Arquitectura de Software**](./documentation/ARCHITECTURE.md): Diagramas de capas, flujo de inicio, servicios de negocio y patrones.
2. 🗄️ [**Diccionario de Datos**](./documentation/DICCIONARIO_DATOS.md): Descripción técnica de tablas SQLite, columnas, claves y almacenamiento seguro.
3. 📋 [**Catálogo de Funcionalidades**](./documentation/FUNCIONALIDADES.md): Desglose exhaustivo de las herramientas disponibles en cada vista.

---

## 💻 Requisitos y Compilación

### Requisitos Previos
* **OS:** Windows 10 / 11
* **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o posterior
* **IDE Recomendado:** Visual Studio 2022 / VS Code / JetBrains Rider

### Pasos para Compilar y Ejecutar

```bash
# Clonar el repositorio
git clone <URL_DEL_REPOSITO>
cd Cajolote_desktop

# Restaurar dependencias y compilar
dotnet build

# Ejecutar la aplicación
dotnet run --project Cajolote.csproj
```
