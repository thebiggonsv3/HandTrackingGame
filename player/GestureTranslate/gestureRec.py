import json
import socket
import time
import cv2
import HandTracking.HandTrackerDetection as htm

HOST = "127.0.0.1"
PORT = 5000

# Accept one Godot connection and forward gestures over the same TCP stream.
server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind((HOST, PORT))
server.listen(1)
print("Waiting for Godot...")
conn, _ = server.accept()
print("Connected to Godot")

capture = cv2.VideoCapture(0)
detector = htm.HandDetector()
last_gesture = None

while True:
    ret, frame = capture.read()
    if not ret:
        continue

    gesture = detector.detect_fingers(frame)
    if gesture is not None and gesture != last_gesture:
        last_gesture = gesture
        # creates dict, preparing to send gesture and timestamp to Godot
        payload = {"type": "gesture", "action": gesture, "time": time.time()}
        try:
            # converts dict to JSON and sends it over TCP stream to Godot
            conn.sendall((json.dumps(payload) + "\n").encode("utf-8"))
        except Exception:
            break

