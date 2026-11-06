# Trabajo Práctico Integrador
## Desarrollo de Software 2026

### Integrantes del grupo: Caceres Xavier - 53140, Barrientos Valentina - 56719, Amado Maximiliano - 56666

API REST (ASP.NET Core, .NET 10) para la gestión de turnos médicos: autenticación de administradores y pacientes con JWT, médicos, especialidades, disponibilidades, reserva y cancelación de turnos y búsquedas avanzadas.

## Estructura de la solución

| Proyecto | Capa |
|---|---|
| `Dsw2026Tpi.Api` | Presentación: controladores, middleware de errores, seguridad (JWT, roles, CORS, rate limiting), logging |
| `Dsw2026Tpi.Application` | Servicios de aplicación y DTOs |
| `Dsw2026Tpi.Domain` | Entidades y reglas de negocio |
| `Dsw2026Tpi.Data` | Persistencia con Entity Framework Core (SQL Server) y migraciones |
| `Dsw2026Tpi.CrossCutting` | Excepciones, códigos de error, roles y utilidades comunes |
| `Dsw2026Tpi.Test` | Pruebas unitarias (xUnit + Moq) |

## Configuración y ejecución local

### 1. Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express o superior)
- Herramienta de EF Core: `dotnet tool install --global dotnet-ef` (o la consola del Administrador de paquetes de Visual Studio)

### 2. Secretos (no se guardan en el repositorio)

La clave de firma de los JWT y la contraseña inicial del administrador se configuran con *User Secrets*, desde la carpeta `Dsw2026Tpi.Api`:

```bash
cd Dsw2026Tpi.Api
dotnet user-secrets set "Jwt:Key" "<clave aleatoria de al menos 32 caracteres>"
dotnet user-secrets set "Admin:Password" "<contraseña del administrador>"
```

- `Jwt:Key` es obligatoria: sin ella la API no arranca. Debe tener al menos 32 caracteres (HMAC-SHA256).
- `Admin:Password` se usa solo la primera vez, para crear el administrador inicial. Debe tener 8 caracteres o más, con mayúscula, minúscula, número y un símbolo.
- Opcional: `Admin:Email` (por defecto `admin@system.com`).
- En otros entornos se pueden usar variables de entorno: `Jwt__Key`, `Admin__Password`.

### 3. Configuración (`Dsw2026Tpi.Api/appsettings*.json`)

- `ConnectionStrings:DefaultConnection`: cadena de conexión. Por defecto usa LocalDB (`(localdb)\MSSQLLocalDB`, base `Dsw2026Tpi`).
- `Jwt`: `Issuer`, `Audience` y `ExpiresInMinutes` (vencimiento del token, 60 minutos).
- `Cors:AllowedOrigins`: orígenes permitidos para el frontend.
- `RateLimiting`: límites de solicitudes por minuto (login de administrador, login de paciente, reserva de turnos y límite general).
- `Dsw2026Tpi.Api/holidays.json`: feriados y días no laborales (formato `YYYY-MM-DD`). En esos días no se generan turnos.

### 4. Base de datos

La solución usa dos contextos: `Dsw2026TpiDbContext` (datos del sistema) y `AuthenticationDbContext` (usuarios y roles de Identity). Desde la raíz del repositorio:

```bash
dotnet ef database update --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api --context AuthenticationDbContext
dotnet ef database update --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api --context Dsw2026TpiDbContext
```

En Visual Studio: `Update-Database -Context AuthenticationDbContext` y `Update-Database -Context Dsw2026TpiDbContext`, con `Dsw2026Tpi.Data` como proyecto predeterminado.

Al aplicar las migraciones se cargan las especialidades iniciales (`Dsw2026Tpi.Data/Sources/specialities.json`) y los roles (`Dsw2026Tpi.Data/Sources/roles.json`).

> La migración `RestriccionesUnicasTurnosYCitas` agrega las restricciones únicas del modelo de datos. Si una base de desarrollo anterior tiene turnos o disponibilidades duplicados, la migración falla. En ese caso hay que eliminar esa base de prueba (`dotnet ef database drop ...`) y volver a aplicar las migraciones.

### 5. Ejecutar

```bash
dotnet run --project Dsw2026Tpi.Api --launch-profile http
```

- Swagger (solo en Development): `http://localhost:5278/swagger`
- Al iniciar, el sistema crea (si no existen) los roles `ADMINISTRADOR` y `PACIENTE` y el usuario administrador con `Admin:Email` / `Admin:Password`.
- Los logs se escriben en consola y en `Logs/log-AAAAMMDD.txt`.
- Las fechas y horas de los turnos se interpretan con la hora local del servidor.

### 6. Pruebas

```bash
dotnet test Dsw2026Tpi.Test/Dsw2026Tpi.Tests.csproj
```

## Uso de la API

- Todos los endpoints requieren un token JWT, excepto `POST /api/auth/admin/login` y `POST /api/auth/patient/login`. El token se envía en el encabezado `Authorization: Bearer <token>`.
- Roles: `ADMINISTRADOR` (operaciones administrativas) y `PACIENTE` (reserva, consulta y cancelación de sus propios turnos). Un paciente solo puede operar con su propio DNI y sus propias citas.
- Fechas en formato `YYYY-MM-DD` y horarios en formato `HH:mm`. Los identificadores son GUID.
- Los listados paginados aceptan `pageSize` y `pageIndex` y responden `{ "pageSize", "pageIndex", "data": [...], "total" }`.
- Las bajas (`DELETE`) son lógicas (columna `deleted`) y responden `200` con el texto `ok`.

### Autenticación — `/api/auth`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/auth/admin/login` | Público | Login de administrador. Body: `{ "email", "password" }`. Responde `{ "token", "role": "ADMINISTRADOR" }`. |
| POST | `/api/auth/patient/login` | Público | Login de paciente. Body: `{ "email", "dni" }` (DNI de 7 u 8 dígitos). Si el paciente no existe, se registra automáticamente con el rol `PACIENTE`. Responde `{ "token", "role": "PACIENTE" }`. |
| POST | `/api/auth/admin/register` | ADMINISTRADOR | Registra otro administrador. Body: `{ "email", "password" }`. |
| POST | `/api/auth/logout` | Autenticado | Cierra la sesión: revoca los tokens emitidos al usuario. Responde `ok`. |

### Especialidades — `/api/specialties`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| GET | `/api/specialties?pageSize=&pageIndex=&name=` | Autenticado | Listado paginado de especialidades activas. `name` (opcional, 3 a 100 caracteres) filtra por nombre. |
| POST | `/api/specialties` | ADMINISTRADOR | Alta. Body: `{ "name", "description" }` (3–100 y 10–100 caracteres; nombre único). Responde `201` con la entidad creada. |
| PUT | `/api/specialties/{id}` | ADMINISTRADOR | Modificación (mismo body). Responde la entidad actualizada. |
| DELETE | `/api/specialties/{id}` | ADMINISTRADOR | Baja lógica. |

### Médicos — `/api/doctors`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| GET | `/api/doctors?pageSize=&pageIndex=&name=` | Autenticado | Listado paginado de médicos activos con su especialidad. `name` opcional (3 a 100 caracteres). |
| GET | `/api/doctors/{id}/availabilities` | Autenticado | Disponibilidad del mes en curso: `[{ "id", "day", "startTime", "endTime" }]`. Vacío si no se cargó. |
| POST | `/api/doctors` | ADMINISTRADOR | Alta. Body: `{ "name", "licenseNumber", "specialtyId" }`. Responde `201` con la entidad creada. |
| PUT | `/api/doctors/{id}` | ADMINISTRADOR | Modificación (mismo body). Responde la entidad actualizada. |
| DELETE | `/api/doctors/{id}` | ADMINISTRADOR | Baja lógica. |

### Disponibilidades — `/api/availabilities`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/availabilities` | ADMINISTRADOR | Define los días y horarios de atención. Genera turnos de 30 minutos para el resto del mes en los días indicados, sin feriados ni horarios pasados. Responde `201` con las disponibilidades creadas. |
| PUT | `/api/availabilities` | ADMINISTRADOR | Mismo body que el POST. Sobrescribe los turnos futuros no reservados; los turnos reservados se conservan. Responde la disponibilidad actualizada. |
| GET | `/api/availabilities?doctorId=&date=YYYY-MM-DD` | Autenticado | Turnos libres de un médico para una fecha: `[{ "id", "startTime", "endTime", "status" }]`. |

Body de POST y PUT:

```json
{
  "doctorId": "GUID",
  "days": [ { "day": "LUNES", "startTime": "09:00", "endTime": "12:00" } ]
}
```

Días válidos: `LUNES`, `MARTES`, `MIÉRCOLES`, `JUEVES`, `VIERNES`, `SÁBADO`, `DOMINGO`. No se permiten horarios superpuestos para el mismo médico.

### Citas — `/api/appointments`

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/appointments` | PACIENTE | Reserva un turno disponible. Responde `201` con la cita creada. Si el turno ya fue reservado, responde `409 APPOINTMENT_CONFLICT`. |
| GET | `/api/appointments/patient?dni=` | PACIENTE | Turnos activos (reservados) del paciente autenticado. |
| DELETE | `/api/appointments/{id}` | PACIENTE | Cancela una cita propia en estado `BOOKED`. El turno vuelve a estar disponible. |
| GET | `/api/appointments?date=YYYY-MM-DD&pageSize=&pageIndex=` | ADMINISTRADOR | Turnos del día, paginados. |
| GET | `/api/appointments/search?pageSize=&pageIndex=&specialtyId=&doctorId=&dni=&date=` | ADMINISTRADOR | Búsqueda combinada (todos los filtros son opcionales). |

Body del POST:

```json
{
  "doctorId": "GUID",
  "availabilitySlotId": "GUID",
  "patient": { "dni": 30111222 },
  "reason": "Control anual"
}
```

Cada elemento de las búsquedas de administrador tiene la forma `{ "appointmentsId", "appointmentsStatus", "patient": { "dni", "fullName" }, "doctor": { "doctorId", "name", "specialty": { "specialtyId", "name" } }, "availableTime" }`.

Estados de una cita: `BOOKED`, `CANCELLED`, `ATTENDED`, `NO_SHOW`.

### Otros

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| GET | `/health-check` | Autenticado | Estado de la API. |

## Errores

Todas las respuestas de error tienen el mismo formato (`details` es opcional):

```json
{
  "errorCode": "APPOINTMENT_CONFLICT",
  "message": "Slot already booked",
  "details": [ { "field": "availabilitySlotId", "issue": "slot_unavailable" } ]
}
```

| Código HTTP | Ejemplos de `errorCode` |
|---|---|
| 400 | `VALIDATION_ERROR`, `INVALID_DAY`, `INVALID_TIME_FORMAT`, `INVALID_TIME_RANGE`, `APPOINTMENT_PAST_DATE`, `APPOINTMENT_CANNOT_BE_CANCELLED`, `SPECIALITY_NOT_FOUND`, `REGISTER_USER_INVALID` |
| 401 | `AUTHENTICATION_FAILED` |
| 403 | `AUTHORIZATION_FAILED` |
| 404 | `ENTITY_NOTFOUND`, `RESOURCE_NOT_FOUND` |
| 405 | `METHOD_NOT_ALLOWED` |
| 409 | `APPOINTMENT_CONFLICT`, `AVAILABILITY_OVERLAP`, `SPECIALITY_CONFLICT`, `REGISTER_USER_CONFLICT`, `CONCURRENCY_CONFLICT` |
| 415 | `UNSUPPORTED_MEDIA_TYPE` |
| 429 | `RATE_LIMIT_EXCEEDED` |
| 500 | `UNHANDLED_ERROR` |

## Rate limiting

Configurable en la sección `RateLimiting` de `appsettings.json`. Las solicitudes rechazadas no se encolan, responden `429` con el formato de error y se registran en el log.

| Política | Límite por defecto |
|---|---|
| Login de administrador | 5 solicitudes por minuto por IP |
| Login de paciente | 10 solicitudes por minuto por IP |
| Reserva de turnos | 5 solicitudes por minuto por paciente |
| Resto de los endpoints | 100 solicitudes por minuto por usuario o IP |
