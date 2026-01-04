# HotelAdmin Microservice

This microservice is an **AWS Lambda function** for hotel administration in the HotelMan system.  
It supports **listing hotels**, **adding new hotels with file uploads**, and publishing **hotel creation events to SNS**.  

---

## 🏗️ Architecture Overview

The microservice interacts with multiple AWS services:

```text
       +----------------+
       | Client / Admin |
       +-------+--------+
               |
               v
     +---------------------+
     |  HotelAdmin Lambda  |
     |  - ListHotels       |
     |  - AddHotel         |
     +---------+-----------+
               |
    +----------+----------+
    |                     |
    v                     v
+--------+           +---------+
| DynamoDB|           |   S3   |
| Hotels  |           | Files  |
+--------+           +---------+
               |
               v
         +--------------+
         | SNS Topic    |
         | HotelCreated |
         +--------------+

📦 Features
1. ListHotels

Endpoint: AWS API Gateway GET

Input: token query parameter (JWT)

Functionality:

Validates JWT token

Extracts userId from token

Fetches hotels associated with the user from DynamoDB

Returns JSON response with hotel list

Handles CORS for browser requests

2. AddHotel

Endpoint: AWS API Gateway POST

Input: multipart/form-data

Hotel details (hotelName, hotelCity, hotelPrice, hotelRating)

File upload (hotel image)

userId and idToken (JWT)

Functionality:

Validates JWT and checks Admin group membership

Uploads file to S3 bucket

Saves hotel details in DynamoDB

Maps hotel to HotelCreatedEvent using AutoMapper

Publishes the event to SNS Topic

Handles CORS and authorization errors

🔑 Security

JWT token-based authorization

Admin-only actions for adding hotels

Unauthorized requests return HTTP 401
