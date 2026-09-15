# Cloud-Based College Resource Booking System

A Cloud Computing case study project.

Students and faculty book college resources — classrooms, computer labs, seminar halls,
projectors, meeting rooms and sports facilities — through a web application.
An administrator approves or rejects the requests, manages the resources, and sees
which resources are used most.

The point of the project is to show, in a working application, how a normal web system
uses the cloud: a **cloud database** (Neon PostgreSQL), a **cloud-hosted API** (Render),
**authentication**, **role-based access control**, and **real-time availability checking**
that prevents double booking.

**Live:** <https://cloudcomputingproject-98vu.onrender.com>
 · API docs: <https://cloudcomputingproject-98vu.onrender.com/swagger>

---

## Features

- User registration (Student / Faculty)
- Login with JWT authentication
- Role-based access control (Student, Faculty, Admin)
- Browse and search resources, each with its own picture
- Book directly from a resource card, without leaving the page
- Real-time availability checking before booking
- Double booking prevention, enforced on the server
- Booking requests with Pending / Approved / Rejected / Cancelled status
- Admin approval and rejection of requests
- Admin resource management (add, edit, deactivate)
- Upload a picture from your own computer, stored in the cloud database
- Animated dashboard counters and cards that appear as you scroll
- Booking history for every user
- Resource utilization report for the admin
- Swagger API documentation
- Responsive interface (laptop, tablet, mobile)

---

## Technologies

| Layer | Technology |
|---|---|
| Frontend | HTML, CSS, vanilla JavaScript (no framework) |
| Backend | C# with ASP.NET Core Web API |
| Data access | Entity Framework Core |
| Database | PostgreSQL |
| Cloud database | Neon |
| Cloud hosting | Render (Docker container) |
| Authentication | JWT (JSON Web Tokens) |
| API documentation | Swagger / OpenAPI |

---

## Architecture

```text
                    Users
                      │
                      ▼
                Web Browser
                      │
                      │ HTTPS
                      ▼
            ┌─────────────────────┐
            │    Web Frontend     │
            │   HTML / CSS / JS   │
            └──────────┬──────────┘
                       │
                       │ REST API  (JSON + JWT token)
                       ▼
            ┌─────────────────────┐
            │  ASP.NET Core API   │
            │         C#          │
            │   hosted on Render  │
            └──────────┬──────────┘
                       │
                       │ PostgreSQL connection (SSL)
                       ▼
            ┌─────────────────────┐
            │   Neon PostgreSQL   │
            │   Cloud Database    │
            └─────────────────────┘
```

The frontend never talks to the database. It only calls the API, and the API is the
only component that holds the database credentials.

---

## Cloud Concepts Demonstrated

**Cloud Database — Neon PostgreSQL**
The data does not live on any college computer. Neon runs a managed PostgreSQL server
that the API reaches over the internet. The same C# code works against a local
PostgreSQL during development and against Neon in production; only the connection
string changes.

**Cloud Hosting — Render**
Render runs the ASP.NET Core API on a server we never have to install or maintain.
It gives the API a public HTTPS address, so anyone can use the system from anywhere
without the college hosting a machine itself.

**Configuration instead of hard-coded values**
Connection strings, the token signing key and the allowed frontend address all come
from configuration. Locally they come from `appsettings.Development.json`; in the cloud
they come from environment variables set on Render. No password is ever written in the
source code, so the repository can be public.

**Authentication**
Authentication answers *who are you*. The user sends email and password, the API checks
the password against a stored hash, and returns a signed JWT token. The browser sends
that token with every later request.

**Authorization / Role-Based Access Control**
Authorization answers *what are you allowed to do*. The role (Student, Faculty, Admin)
is stored inside the token. Admin-only endpoints are marked
`[Authorize(Roles = "Admin")]`, so a student calling them gets `403 Forbidden` even if
they call the API directly.

**Real-time availability checking**
When a user picks a resource, date and time, the API queries the cloud database at that
moment to see whether the slot is free, and answers `{"available": true}` or
`{"available": false}`.

---

## Database Structure

```text
   User                      Resource
   ────────────              ────────────
   Id (PK)                   Id (PK)
   FullName                  Name
   Email (unique)            Type
   PasswordHash              Location
   Role                      Capacity
   CreatedAt                 Description
      │                      ImageUrl
      │ 1                    IsActive
      │                      CreatedAt
      │                         │ 1
      │ many                    │ many
      ▼                         ▼
              Booking
              ────────────
              Id (PK)
              UserId     (FK → User)
              ResourceId (FK → Resource)
              BookingDate
              StartTime
              EndTime
              Purpose
              Status
              CreatedAt
```

- One user can have many bookings.
- One resource can have many bookings.
- A booking belongs to exactly one user and one resource.

`Role` is stored as text (`Student`, `Faculty`, `Admin`) and
`Status` as text (`Pending`, `Approved`, `Rejected`, `Cancelled`), so the tables are
readable when you open the database directly.

### Pictures

`Resource.ImageUrl` stores the **address** of a picture, never the picture itself.
Three kinds of address are supported, and the interface works out which is which:

| Stored value | Where the picture lives |
|---|---|
| `images/resources/classroom.svg` | a ready-made picture shipped with the website |
| `/api/images/7` | a picture the admin uploaded, kept in the cloud database |
| `https://...` | any picture elsewhere on the internet |

If a resource has no picture, the interface falls back to
`images/resources/default.svg`, so a card is never blank.

**Uploaded pictures** are stored in the `UploadedImages` table, in a PostgreSQL
`bytea` (binary) column, and served back by `GET /api/images/{id}`.

A real company would normally put the file in cloud *file* storage (Amazon S3, Azure
Blob Storage) and keep only the link in the database, because databases are tuned for
small pieces of text rather than large files. We keep the bytes in Neon on purpose:
the whole system then needs only **two** cloud services — Render for the API and Neon
for the data — which is much easier to set up and to explain. The `https://...` option
is there to show how the project would point at real file storage instead.

Safety rules on upload: administrators only, maximum 2 MB, and only PNG / JPG / WEBP /
GIF. The API also reads the first bytes of the file to confirm it really is an image,
because a file can always be renamed. SVG uploads are refused on purpose — an SVG file
can contain scripts, and we do not want to serve somebody else's script from our own
API. (The ready-made pictures are SVG, but those are part of the project, not uploads.)

---

## How Double Booking Is Prevented

Two time ranges overlap when:

```text
existing.StartTime < requested.EndTime
AND
existing.EndTime   > requested.StartTime
```

Only bookings with status **Pending** or **Approved** block a resource.
**Rejected** and **Cancelled** bookings free the slot again.

Example — an existing booking for Computer Lab 1 from **10:00 to 12:00**:

| Requested slot | Result | Why |
|---|---|---|
| 11:00 – 13:00 | ✗ Blocked | 10:00 < 13:00 **and** 12:00 > 11:00 |
| 09:00 – 11:00 | ✗ Blocked | 10:00 < 11:00 **and** 12:00 > 09:00 |
| 12:00 – 14:00 | ✓ Allowed | 12:00 > 12:00 is false — back-to-back is fine |
| 08:00 – 10:00 | ✓ Allowed | ends exactly when the other starts |

The check lives in `backend/Services/BookingService.cs` and runs **on the server**.
The browser also calls it for quick feedback, but the server checks again inside
`POST /api/bookings`, so a booking can never be created by skipping the web page.
The admin's approve action checks once more, in case two pending requests for the same
slot are waiting.

---

## API Endpoints

Every endpoint except register and login needs the header
`Authorization: Bearer <token>`.

### Authentication

| Method | Endpoint | Who | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Anyone | Create a Student or Faculty account |
| POST | `/api/auth/login` | Anyone | Log in, returns a JWT token |

### Resources

| Method | Endpoint | Who | Description |
|---|---|---|---|
| GET | `/api/resources` | Any user | List resources (`?includeInactive=true` for admin) |
| GET | `/api/resources/{id}` | Any user | One resource |
| POST | `/api/resources` | Admin | Add a resource (including its picture) |
| PUT | `/api/resources/{id}` | Admin | Edit a resource or change its picture |
| DELETE | `/api/resources/{id}` | Admin | Delete, or deactivate if it already has bookings |

### Bookings

| Method | Endpoint | Who | Description |
|---|---|---|---|
| GET | `/api/bookings/availability` | Any user | Is the slot free? Returns `{ "available": true/false }` |
| GET | `/api/bookings/busy` | Any user | Slots already taken on a date |
| POST | `/api/bookings` | Any user | Create a Pending booking |
| GET | `/api/bookings/my` | Any user | My booking history |
| GET | `/api/bookings/{id}` | Owner or admin | One booking |
| GET | `/api/bookings/stats` | Any user | Counters (own for users, all for admin) |
| GET | `/api/bookings` | Admin | All bookings (`?status=Pending`) |
| PUT | `/api/bookings/{id}/approve` | Admin | Approve a request |
| PUT | `/api/bookings/{id}/reject` | Admin | Reject a request |
| PUT | `/api/bookings/{id}/cancel` | Owner or admin | Cancel a booking |

### Images

| Method | Endpoint | Who | Description |
|---|---|---|---|
| POST | `/api/images` | Admin | Upload a picture (form-data, max 2 MB) |
| GET | `/api/images/{id}` | Anyone | Show a picture |

`GET` is open to everyone on purpose: a browser loading `<img src="/api/images/7">`
cannot attach the login token, and a photograph of a classroom is not secret.

### Reports

| Method | Endpoint | Who | Description |
|---|---|---|---|
| GET | `/api/reports/utilization` | Admin | Bookings and hours per resource |

### Utility

| Method | Endpoint | Description |
|---|---|---|
| GET | `/` | API name and status |
| GET | `/health` | Health check |
| GET | `/swagger` | Interactive API documentation |

**Example availability call**

```text
GET /api/bookings/availability?resourceId=3&date=2026-09-20&startTime=10:00&endTime=12:00
```

```json
{
  "available": false,
  "message": "Computer Lab 1 is not available during this time. It is already booked.",
  "conflictingSlots": [
    { "startTime": "10:00:00", "endTime": "12:00:00", "status": "Approved" }
  ]
}
```

---

## Project Structure

```text
cloud-resource-booking/
│
├── backend/                        ASP.NET Core Web API (C#)
│   ├── Controllers/
│   │   ├── AuthController.cs       register / login
│   │   ├── ResourcesController.cs  resources + utilization report
│   │   ├── BookingsController.cs   availability, bookings, approve/reject
│   │   └── ImagesController.cs     upload and serve resource pictures
│   ├── Models/                     database entities
│   │   ├── User.cs  UserRole.cs
│   │   ├── Resource.cs
│   │   └── Booking.cs  BookingStatus.cs
│   ├── DTOs/                       shapes the API accepts and returns
│   ├── Data/
│   │   ├── AppDbContext.cs         EF Core mapping and relationships
│   │   └── DbSeeder.cs             demo admin, users and resources
│   ├── Services/
│   │   ├── PasswordHasher.cs       PBKDF2 password hashing
│   │   ├── TokenService.cs         creates the JWT token
│   │   └── BookingService.cs       availability / overlap logic
│   ├── Migrations/                 EF Core migrations
│   ├── Program.cs                  startup: database, auth, CORS, Swagger
│   ├── Dockerfile                  how Render builds and runs the API
│   ├── appsettings.json
│   └── appsettings.Development.example.json
│
├── frontend/                       static website
│   ├── index.html                  redirects to login or dashboard
│   ├── login.html  register.html
│   ├── dashboard.html              student / faculty home
│   ├── resources.html              browse + book inline
│   ├── my-bookings.html
│   ├── admin-dashboard.html  admin-bookings.html
│   ├── admin-resources.html  utilization.html
│   ├── css/style.css
│   ├── images/resources/           the ready-made pictures (SVG)
│   └── js/
│       ├── config.js               ← the API URL lives only here
│       ├── api.js                  fetch wrapper + token header
│       ├── auth.js                 session handling + page guards
│       ├── layout.js               sidebar + shared helpers
│       ├── resource-card.js        picture cards + inline booking panel
│       ├── motion.js               counting numbers + scroll animations
│       └── (one file per page)
│
├── .gitignore
├── .env.example
└── README.md
```

Student and faculty share one `dashboard.html`, because their permissions are the same.
The heading and the menu are built from the role at runtime.

---

## How the Frontend Talks to the Backend

The API address is written in **one file only**: `frontend/js/config.js`.

The page checks its own address and picks the matching API, so the same files
work on your machine and in the cloud without editing anything when you switch:

```js
const PRODUCTION_API_URL = "https://cloudcomputingproject-98vu.onrender.com/api";
const LOCAL_API_URL      = "http://localhost:5080/api";

const isLocalMachine =
  location.hostname === "localhost" || location.hostname === "127.0.0.1";

const CONFIG = {
  API_BASE_URL: isLocalMachine ? LOCAL_API_URL : PRODUCTION_API_URL
};
```

Every page uses the helper in `frontend/js/api.js`, which adds the token automatically:

```js
const resources = await api.get("/resources");
```

Behind the scenes that sends:

```text
GET http://localhost:5080/api/resources
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

After deployment you change that one line to the Render URL, and the whole frontend
points at the cloud.

---

## Local Setup

### Requirements

- .NET SDK 10.0 (the version the project targets)
- PostgreSQL running locally
- Python 3 (only to serve the frontend files)

### 1. Create the local database

```bash
psql -d postgres -c "CREATE ROLE booking_user LOGIN PASSWORD 'booking_pass';"
```

```bash
psql -d postgres -c "CREATE DATABASE resource_booking OWNER booking_user;"
```

### 2. Create your local settings file

```bash
cp backend/appsettings.Development.example.json backend/appsettings.Development.json
```

This file is in `.gitignore`, so your local password is never committed.
Edit it if your PostgreSQL username, password or port is different.

### 3. Run the backend

```bash
cd backend && dotnet restore && dotnet run
```

The API starts on <http://localhost:5080>.
On the first start it creates the tables and inserts the demo data automatically.

Swagger: <http://localhost:5080/swagger>

If you prefer to apply the migrations yourself:

```bash
cd backend && dotnet ef database update
```

### 4. Run the frontend

In a second terminal:

```bash
cd frontend && python3 -m http.server 5500
```

Open <http://localhost:5500>.

> Do not open the HTML files directly with `file:///` — the browser then blocks the
> API calls. Always use the local server.

---

## Demo Accounts

These accounts are created by the seeder for demonstration.
Change or remove them before any real use.

| Role | Email | Password |
|---|---|---|
| Admin | `admin@college.com` | `Admin123!` |
| Student | `student@college.com` | `Demo123!` |
| Faculty | `faculty@college.com` | `Demo123!` |

New accounts can only be Student or Faculty. The public registration page refuses to
create an Admin, so the only administrator is the one created by the seeder.

Seeded resources: Classroom 101, Classroom 102, Computer Lab 1, Computer Lab 2,
Seminar Hall, Projector 1, Meeting Room A, Basketball Court.

---

## Try It Out

1. Log in as the student. The dashboard shows a picture card for every resource.
2. Click the *Computer Lab 1* picture. A small booking form opens under the card.
3. Choose a date, `10:00`–`12:00`, write a purpose, press **Check Availability** →
   green, so **Book Now** becomes clickable. Book it.
4. Log out, log in as the faculty user, and try *Computer Lab 1* on the same date
   from `11:00`–`13:00` → red: the times overlap, and booking is blocked.
5. Try `12:00`–`14:00` instead → green, because back-to-back bookings are allowed.
6. Log in as the admin, approve one request and reject the other.
7. The rejected slot is free again — the user can book it once more.
8. In **Manage Resources**, press *Edit* on any resource. Either click a different
   ready-made picture, or drag a photo from your computer onto the upload box.
   Press *Save Changes* — the new picture appears everywhere at once.
9. Open **Utilization Report** to see which resources are booked most.

---

## Interface Notes

The interface uses a small amount of motion, all of it in `frontend/js/motion.js`:

- dashboard numbers count up from zero when a page opens,
- resource cards fade and slide in as you scroll to them,
- the utilization bars grow to their real width,
- cards lift slightly and their picture zooms a little under the pointer.

Two rules were followed so the animation never gets in the way:

1. **Nothing is hidden waiting for an animation.** The cards are visible by default;
   JavaScript is what temporarily hides them just before animating. If JavaScript does
   not run, the whole page still shows normally.
2. **The visitor's setting wins.** Anyone whose system asks for reduced motion sees
   the same interface with the animation switched off (`prefers-reduced-motion`).

## Deployment

```text
GitHub  →  Neon PostgreSQL  →  Render
```

### Why the backend needs Docker

Render runs Node, Python, Ruby, Go, Rust and Elixir by itself, but it has **no
built-in support for .NET**. For .NET it runs a *container* instead, so the project
includes `backend/Dockerfile` — a short recipe telling Render how to build and start
the API. Render reads that file automatically; there is no build or start command to
type.

The Dockerfile is written in two stages: the first uses the full .NET SDK to compile
the project, and the second keeps only the smaller ASP.NET runtime plus the compiled
output, so the image that actually runs stays small.

### 1. Neon — the cloud database

1. Create a free project at [neon.tech](https://neon.tech).
2. Copy the connection string from the dashboard. It looks like
   `Host=ep-xxx.neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=VerifyFull;Channel Binding=Require`
   (Neon's `postgresql://...` URL form works too — the API converts it.)

   > Neon displays the value inside a C# snippet, so what you copy may look like
   > `"Host=...;Channel Binding=Require;",` — **paste only the part between the
   > quotation marks**, with no quotes and no trailing comma. The API strips them
   > if they slip through, and tells you plainly if the value still cannot be read.
3. Keep it out of the repository. It only ever goes into Render's environment
   variables.

You do **not** have to create the tables yourself. The API applies its migrations and
inserts the demo data the first time it starts.

### 2. Render — the backend API

Create a **Web Service** from the GitHub repository:

| Setting | Value |
|---|---|
| Language / Runtime | **Docker** |
| Root Directory | `backend` |
| Dockerfile Path | `backend/Dockerfile` |
| Build / Start command | *leave empty — the Dockerfile handles both* |

Environment variables (Render → Environment):

| Key | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | the Neon connection string |
| `Jwt__Key` | any long random text, **at least 32 characters** (required) |
| `Jwt__Issuer` | `ResourceBookingApi` |
| `Seed__AdminEmail` | `admin@college.com` |
| `Seed__AdminPassword` | the password for the seeded admin account |
| `Cors__AllowedOrigins__0` | the frontend address (filled in at step 4) |

`PORT` is set by Render automatically, and `Program.cs` already listens on it.

> `ConnectionStrings__DefaultConnection` and `Jwt__Key` are both **required**. They are
> deliberately left empty in `appsettings.json` so no secret is ever committed, which
> means the service cannot start until you set them on Render. If either is missing the
> API stops immediately and says which one — it does not start in a half-working state.
>
> Generate a key with `openssl rand -base64 48`.

When the service is live, check `https://<your-api>.onrender.com/health` and
`https://<your-api>.onrender.com/swagger`.

> On Render's free plan the service sleeps when unused, so the first request after a
> pause can take around a minute. That is normal — worth knowing before a live demo.

### 3. Render — the frontend

Create a **Static Site** from the same repository:

| Setting | Value |
|---|---|
| Root Directory | `frontend` |
| Build Command | *leave empty* |
| Publish Directory | `.` |

### 4. Connect the two

Both addresses only exist after the services are created, so this is the last step.

1. In `frontend/js/config.js`, set `PRODUCTION_API_URL` to your API address and
   push the change. Nothing else needs editing — the file already falls back to
   `localhost` when you run it on your own machine.

2. In the backend service on Render, set `Cors__AllowedOrigins__0` to the static site
   address, for example `https://<your-frontend>.onrender.com`, and let the service
   restart.

CORS is what allows the browser to call an API on a different address. If the frontend
address is missing from that list, every request is blocked by the browser — that is
the usual cause of "it worked locally but not after deploying".

### Keeping secrets out of the repository

`.gitignore` excludes `appsettings.Development.json`, so local database passwords are
never published. Production values live only in Render's environment variables.
`.env.example` lists the names that have to be set, with no values.

If a real password is ever pasted somewhere public, change it: in Neon, reset the role
password and update `ConnectionStrings__DefaultConnection` on Render.
