#!/bin/bash

# Run both backend and frontend dev servers
# Press Ctrl+C to stop both

cleanup() {
    echo ""
    echo "Stopping servers..."

    # Stop frontend first
    if [ -n "$FRONTEND_PID" ] && kill -0 $FRONTEND_PID 2>/dev/null; then
        echo "Stopping frontend (PID $FRONTEND_PID)..."
        kill $FRONTEND_PID 2>/dev/null
        wait $FRONTEND_PID 2>/dev/null
        echo "Frontend stopped."
    fi

    # Then stop backend
    if [ -n "$BACKEND_PID" ] && kill -0 $BACKEND_PID 2>/dev/null; then
        echo "Stopping backend (PID $BACKEND_PID)..."
        kill $BACKEND_PID 2>/dev/null
        wait $BACKEND_PID 2>/dev/null
        echo "Backend stopped."
    fi

    echo "Done."
    exit 0
}

trap cleanup SIGINT SIGTERM

echo "Starting Photo Booth development servers..."
echo ""

# Start backend
echo "Starting backend (dotnet) on http://localhost:5192..."
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
echo "Open http://localhost:5173 in your browser."
echo "Press Ctrl+C to stop both servers."
echo ""

# Wait for either to exit
wait $BACKEND_PID $FRONTEND_PID
