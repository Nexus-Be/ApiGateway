- success:
  {
  "success": true,
  "statusCode": 200,
  "message": "User retrieved successfully.",
  "data": {
  "id": "usr_9981",
  "fullName": "Nguyen Van A",
  "email": "vana@example.com",
  "status": "Active"
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
  }
- phân trang:
  {
  "success": true,
  "statusCode": 200,
  "data": {
  "items": [
  { "id": 101, "orderCode": "ORD-001", "amount": 250000 },
  { "id": 102, "orderCode": "ORD-002", "amount": 500000 }
  ],
  "pagination": {
  "pageIndex": 1,
  "pageSize": 20,
  "totalCount": 245,
  "totalPages": 13,
  "hasPreviousPage": false,
  "hasNextPage": true
  }
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
  }
- thất bại:
  {
  "type": "https://errors.yourdomain.com/validation-error",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "detail": "Please refer to the errors property for additional details.",
  "errors": {
  "Email": [
  "The Email field is not a valid e-mail address."
  ],
  "Password": [
  "The Password must be at least 8 characters long.",
  "The Password must contain at least one uppercase letter."
  ]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
  }
