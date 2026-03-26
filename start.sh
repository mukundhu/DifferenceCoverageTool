#!/bin/bash
echo "Starting DiffCoverage Dashboard..."
cd dashboard || exit
echo "Installing Node.js dependencies..."
npm install
echo "Starting Node.js server..."
node server.js
