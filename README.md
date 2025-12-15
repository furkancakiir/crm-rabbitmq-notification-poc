# CRM-like Async Notification System (RabbitMQ + .NET)

This repository demonstrates a **CRM-style asynchronous notification architecture**
using **RabbitMQ**, **ASP.NET Minimal APIs**, and **SQL Server**.

The goal is to simulate how email/SMS/notification workloads can be **queued,
processed asynchronously, and tracked**, similar to enterprise CRM systems.

---

## 🧩 Architecture Overview

Client (Postman / CRM / Power Automate)
|
v
Notification API
|
| (enqueue)
v
RabbitMQ Queue
|
v
Email Worker
|
v
SQL Server (Send Logs)



---

## 📦 Projects

### 1️⃣ NotificationApi
- Accepts email requests via REST API
- Persists initial status as `Queued`
- Publishes messages to RabbitMQ

Endpoints:
- `POST /api/email/enqueue`
- `GET  /api/email/status/{messageId}`
- `GET  /api/email/recent`

---

### 2️⃣ EmailWorker
- Consumes messages from RabbitMQ
- Simulates email sending
- Updates status to `Processing`, `Sent`, or `Failed`

---

### 3️⃣ Shared
- Shared contracts and infrastructure
- `EmailMessage`
- `MsSqlEmailLogStore`
- SQL table creation scripts

---

## 🛠 Technologies Used

- .NET 8 (Minimal API)
- RabbitMQ
- SQL Server (Express compatible)
- Microsoft.Data.SqlClient

---

## 🎯 Why RabbitMQ in CRM Projects?

RabbitMQ is ideal for:
- Email/SMS sending
- External API calls
- Background workflows
- Retry & fault-tolerant processing

This avoids **blocking CRM transactions** and improves scalability.

---

## 🚀 How to Run

1. Start RabbitMQ (local)
2. Run SQL script under `/Shared/Database`
3. Start `EmailWorker`
4. Start `NotificationApi`
5. Send request via Postman

---

## 📝 Disclaimer

This is a **learning & demonstration project**.
Actual email delivery is simulated.
