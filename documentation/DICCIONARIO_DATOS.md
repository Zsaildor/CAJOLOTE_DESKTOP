# Diccionario de Datos - Proyecto Cajolote

Este documento contiene la especificación y descripción técnica de la estructura de la base de datos local utilizada por la aplicación **Cajolote**.

---

## 🛠️ Información General de la Base de Datos

* **Motor de Base de Datos:** SQLite
* **Ubicación del Archivo:** `%LocalAppData%\cajolote.db` (ej. `C:\Users\<Usuario>\AppData\Local\cajolote.db`)
* **ORM:** Entity Framework Core 8.0.14
* **Espacio de Nombres del Modelo:** `Cajolote.Models`
* **DbContext:** [CajoloteDbContext.cs](../Data/CajoloteDbContext.cs)

---

## 📋 Resumen de Tablas (DbSets)

| Nombre de Tabla (SQLite) | Clase Modelo C# | Descripción |
| :--- | :--- | :--- |
| **`Categories`** | [Category](../Models/Category.cs) | Categorías para clasificar los productos. |
| **`Products`** | [Product](../Models/Product.cs) | Catálogo de productos disponibles para la venta. |
| **`Sales`** | [Sale](../Models/Sale.cs) | Registro de cabeceras de ventas realizadas. |
| **`SaleDetails`** | [SaleDetail](../Models/SaleDetail.cs) | Detalle de los productos y cantidades por cada venta. |
| **`HistoricalSales`** | [HistoricalSale](../Models/HistoricalSale.cs) | Historial archivado de ventas antiguas (mantiene IDs fijos). |
| **`HistoricalSaleDetails`** | [HistoricalSaleDetail](../Models/HistoricalSaleDetail.cs) | Detalle de las ventas del historial archivado. |
| **`Notes`** | [Note](../Models/Note.cs) | Notas de deudas o cuentas por cobrar asignadas a clientes. |
| **`ManualDebts`** | [ManualDebt](../Models/ManualDebt.cs) | Ajustes o cargos de deuda manuales cargados a una nota de cliente. |
| **`StoreProfiles`** | [StoreProfile](../Models/StoreProfile.cs) | Información de perfil de la tienda/negocio (configuración local). |

---

## 🔍 Estructura Detallada de Tablas

### 1. Tabla: `Categories`
* **Modelo C#:** `Category`
* **Propósito:** Almacenar las categorías de agrupación de productos.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único de la categoría. |
| **`Name`** | `TEXT` | `string` | Requerido, No nulo | Nombre descriptivo de la categoría. |

#### Relaciones y Propiedades de Navegación
* **`Products`** (`ICollection<Product>`): Colección de productos asociados a esta categoría (Relación Uno a Muchos).

---

### 2. Tabla: `Products`
* **Modelo C#:** `Product`
* **Propósito:** Gestionar el catálogo de artículos y productos de la tienda.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único del producto. |
| **`Barcode`** | `TEXT` | `string?` | Nulable, **Index Único** | Código de barras del producto (para lector). |
| **`Name`** | `TEXT` | `string` | Requerido, No nulo | Nombre del producto. |
| **`Price`** | `decimal(18,2)` | `decimal` | Requerido, No nulo | Precio de venta al público. |
| **`CategoryId`** | `INTEGER` | `int` | FK (`Categories.Id`), Requerido | ID de la categoría a la que pertenece. |
| **`IsQuickProduct`** | `INTEGER` | `bool` | Requerido (0/1), default `false` | Indica si el producto aparece en el panel de acceso rápido del POS. |
| **`IsBulk`** | `INTEGER` | `bool` | Requerido (0/1), default `false` | Indica si el producto se vende a granel/peso (kg) o por unidad. |
| **`IconKey`** | `TEXT` | `string?` | Nulable | Clave del ícono visual asociado al producto. |

#### Relaciones y Propiedades de Navegación
* **`Category`** (`Category`): Categoría a la que pertenece el producto.
* *Nota:* Si se elimina una categoría, Entity Framework aplica borrado en cascada (`OnDelete(DeleteBehavior.Cascade)`) eliminando sus productos asociados.

---

### 3. Tabla: `Sales`
* **Modelo C#:** `Sale`
* **Propósito:** Registrar el encabezado de las transacciones de venta.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único de la venta. |
| **`Date`** | `TEXT` | `DateTime` | Requerido, default `DateTime.Now` | Fecha y hora en la que se realizó la venta. |
| **`Total`** | `decimal(18,2)` | `decimal` | Requerido, No nulo | Monto total de la venta. |
| **`IsSynced`** | `INTEGER` | `bool` | Requerido (0/1), default `false` | Indica si la venta se sincronizó con el servidor/nube. |
| **`IsPaid`** | `INTEGER` | `bool` | Requerido (0/1), default `true` | Indica si la venta ya fue liquidada/pagada. |
| **`NoteId`** | `INTEGER` | `int?` | FK (`Notes.Id`), Nulable | ID de la nota de cliente si la venta se cargó a una cuenta/deuda. |
| **`IsEdited`** | `INTEGER` | `bool` | Requerido (0/1), default `false` | Indica si la venta ha sido editada. |
| **`EditedAt`** | `TEXT` | `DateTime?` | Nulable | Fecha y hora de la última edición de la venta. |

#### Relaciones y Propiedades de Navegación
* **`Note`** (`Note?`): Nota de cliente asociada a esta venta (opcional).
* **`Details`** (`ICollection<SaleDetail>`): Colección de filas de detalle de esta venta.

---

### 4. Tabla: `SaleDetails`
* **Modelo C#:** `SaleDetail`
* **Propósito:** Detallar cada artículo vendido dentro de una venta.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único del renglón de detalle. |
| **`SaleId`** | `INTEGER` | `int` | FK (`Sales.Id`), Requerido | ID de la venta a la que pertenece. |
| **`ProductId`** | `INTEGER` | `int` | FK (`Products.Id`), Requerido | ID del producto vendido. |
| **`Quantity`** | `decimal(18,3)` | `decimal` | Requerido, No nulo | Cantidad vendida (acepta decimales para productos a granel/kg). |
| **`UnitPrice`** | `decimal(18,2)` | `decimal` | Requerido, No nulo | Precio unitario al momento de realizar la venta. |
| **`Subtotal`** | `decimal(18,2)` | `decimal` | Requerido, No nulo | Subtotal calculado (`Quantity * UnitPrice`). |

#### Relaciones y Propiedades de Navegación
* **`Sale`** (`Sale`): Encabezado de la venta asociada.
* **`Product`** (`Product`): Entidad del producto correspondiente.

---

### 5. Tabla: `HistoricalSales`
* **Modelo C#:** `HistoricalSale`
* **Propósito:** Almacenar ventas antiguas archivadas para mantener la fluidez de la tabla activa `Sales`.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK (Mantiene ID original) | Conserva el mismo `Id` que tenía en la tabla `Sales`. |
| **`Date`** | `TEXT` | `DateTime` | Requerido | Fecha original de la venta. |
| **`Total`** | `decimal(18,2)` | `decimal` | Requerido | Monto total original de la venta. |
| **`IsSynced`** | `INTEGER` | `bool` | Requerido (0/1) | Estado de sincronización en nube. |
| **`IsPaid`** | `INTEGER` | `bool` | Requerido (0/1) | Estado de liquidación. |
| **`NoteId`** | `INTEGER` | `int?` | Nulable | Referencia opcional a ID de Nota de crédito. |
| **`IsEdited`** | `INTEGER` | `bool` | Requerido (0/1) | Estado de edición. |
| **`EditedAt`** | `TEXT` | `DateTime?` | Nulable | Fecha de edición. |
| **`ArchivedAt`** | `TEXT` | `DateTime` | Requerido, default `DateTime.Now` | Fecha y hora en la que fue archivada la venta. |

#### Relaciones y Propiedades de Navegación
* **`Details`** (`ICollection<HistoricalSaleDetail>`): Detalles archivados asociados a esta venta histórica.

---

### 6. Tabla: `HistoricalSaleDetails`
* **Modelo C#:** `HistoricalSaleDetail`
* **Propósito:** Detalle de productos de ventas en el archivo histórico.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK (Mantiene ID original) | Conserva el mismo `Id` que tenía en `SaleDetails`. |
| **`HistoricalSaleId`**| `INTEGER` | `int` | FK (`HistoricalSales.Id`) | ID de la venta histórica asociada. |
| **`ProductId`** | `INTEGER` | `int` | FK (`Products.Id`), Requerido | ID del producto vendido. |
| **`Quantity`** | `decimal(18,3)` | `decimal` | Requerido | Cantidad vendida. |
| **`UnitPrice`** | `decimal(18,2)` | `decimal` | Requerido | Precio unitario al momento de la venta. |
| **`Subtotal`** | `decimal(18,2)` | `decimal` | Requerido | Subtotal del renglón. |

---

### 7. Tabla: `Notes`
* **Modelo C#:** `Note`
* **Propósito:** Cuentas fiadas o notas de crédito asociadas a un cliente.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único de la nota. |
| **`ClientName`** | `TEXT` | `string` | Requerido | Nombre del cliente titular de la nota/cuenta. |
| **`CreatedAt`** | `TEXT` | `DateTime` | Requerido, default `DateTime.Now` | Fecha de aperturado de la nota. |
| **`IsPaid`** | `INTEGER` | `bool` | Requerido (0/1), default `false` | Indica si la cuenta ya fue completamente liquidada. |
| **`PaidAmount`** | `decimal(18,2)` | `decimal` | Requerido, default `0.0` | Suma abonada acumulada a la cuenta por el cliente. |

#### Relaciones y Propiedades de Navegación
* **`Sales`** (`ICollection<Sale>`): Ventas vinculadas a esta nota.
* **`ManualDebts`** (`ICollection<ManualDebt>`): Cargos manuales adicionales a la nota.

---

### 8. Tabla: `ManualDebts`
* **Modelo C#:** `ManualDebt`
* **Propósito:** Registrar cargos o ajustes manuales de dinero en la cuenta de un cliente.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único del cargo manual. |
| **`NoteId`** | `INTEGER` | `int` | FK (`Notes.Id`), Requerido | ID de la nota a la que se le asigna el cargo. |
| **`Concept`** | `TEXT` | `string` | Requerido | Concepto o motivo del dinero cargado. |
| **`Amount`** | `decimal(18,2)` | `decimal` | Requerido | Monto a cargar a la deuda. |
| **`CreatedAt`** | `TEXT` | `DateTime` | Requerido, default `DateTime.Now` | Fecha de creación del cargo. |

---

### 9. Tabla: `StoreProfiles`
* **Modelo C#:** `StoreProfile`
* **Propósito:** Guardar los datos de configuración del perfil del establecimiento/tienda.

#### Columnas
| Columna (SQLite) | Tipo SQLite | Tipo C# | Restricciones / Atributos | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **`Id`** | `INTEGER` | `int` | PK, Auto-incremental | Identificador único del registro de perfil. |
| **`StoreName`** | `TEXT` | `string` | Requerido | Nombre comercial de la tienda/negocio. |
| **`Address`** | `TEXT` | `string?` | Nulable | Dirección o ubicación física. |
| **`Phone`** | `TEXT` | `string?` | Nulable | Teléfono de contacto. |
| **`CustomMessage`**| `TEXT` | `string?` | Nulable | Mensaje final impreso en los tickets de compra. |
| **`ImagePath`** | `TEXT` | `string?` | Nulable | Ruta local de la imagen del logotipo guardado. |
| **`LastUpdated`** | `TEXT` | `DateTime` | Requerido | Fecha de última actualización del perfil. |
| **`IsDirty`** | `INTEGER` | `bool` | Requerido (0/1), default `false` | Indica si hay cambios locales pendientes de sincronizar con Firestore. |

---

## 🔒 Almacenamiento Cifrado Fuera de SQLite

Aparte de la base de datos SQLite, existe un archivo binario cifrado mediante la API de DPAPI de Windows:
* **Ubicación:** `%LocalAppData%\session.dat`
* **Modelo C#:** [LocalSession](../Models/LocalSession.cs)
* **Contenido:** Tokens JWT de Firebase Auth, email de usuario, UID y vencimiento de token.
