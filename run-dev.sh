#!/bin/bash

# Run both backend and frontend dev servers
# Press Ctrl+C to stop both

cleanup() {
    echo ""
    echo "Stopping servers..."
    kill $BACKEND_PID $FRONTEND_PID 2>/dev/null
    wait $BACKEND_PID $FRONTEND_PID 2>/dev/null
    echo "Done."
    exit 0
}

trap cleanup SIGINT SIGTERM

echo "Starting Photo Booth development servers..."
echo ""

# Start backend
echo "Starting backend (dotnet) on http://localhost:5000..."
dotnet run --project src/PhotoBooth.Server &
BACKEND_PID=$!

# Start frontend
echo "Starting frontend (vite) on http://localhost:5173..."
cd src/PhotoBooth.Web && pnpm run dev &
FRONTEND_PID=$!

echo ""
echo "Backend PID: $BACKEND_PID"
echo "Frontend PID: $FRONTEND_PID"
echo ""
echo "Press Ctrl+C to stop both servers."
echo ""

# Wait for either to exit
wait $BACKEND_PID $FRONTEND_PID
