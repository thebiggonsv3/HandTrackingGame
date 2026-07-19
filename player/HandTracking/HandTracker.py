import os
import subprocess
import sys
import cv2
import time
import json
import socket
import threading

# Ensure the project root is on sys.path so `player.HandTracking` imports work
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if ROOT_DIR not in sys.path:
    sys.path.insert(0, ROOT_DIR)

PID_FILE = os.path.join(os.path.dirname(__file__), "handtracker.pid")
if os.path.exists(PID_FILE):
    try:
        with open(PID_FILE, "r", encoding="utf-8") as pidfile:
            old_pid = int(pidfile.read().strip())
        if old_pid != os.getpid():
            subprocess.run(["taskkill", "/PID", str(old_pid), "/F"], capture_output=True, text=True)
    except Exception:
        pass

with open(PID_FILE, "w", encoding="utf-8") as pidfile:
    pidfile.write(str(os.getpid()))

import player.HandTracking.HandTrackerDetection as htm

pTime = 0
cTime = 0
capture = cv2.VideoCapture(0)
if not capture.isOpened():
    print('HandTracker: ERROR opening camera device 0', flush=True)
detector = htm.HandDetector()

VERBOSE = False

# Simple TCP server to send gesture messages to clients (Godot connects on 127.0.0.1:5000)
server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind(('127.0.0.1', 5000))
server.listen(5)
server.setblocking(False)
print('HandTracker: listening on 127.0.0.1:5000', flush=True)
clients = []
clients_lock = threading.Lock()

last_action = None
last_action_time = 0.0


def accept_clients():
    while True:
        try:
            conn, addr = server.accept()
            conn.setblocking(False)
            with clients_lock:
                clients.append(conn)
            print(f'HandTracker: accepted connection from {addr} clients={len(clients)}', flush=True)
        except BlockingIOError:
            time.sleep(0.1)
            continue
        except Exception as ex:
            print(f'HandTracker: accept error: {ex}', flush=True)
            time.sleep(0.1)


accept_thread = threading.Thread(target=accept_clients, daemon=True)
accept_thread.start()


def send_gesture(action):
    global last_action, last_action_time
    now = time.time()
    if action != last_action or now - last_action_time > 0.5:
        print(f'HandTracker: send_gesture {action} clients={len(clients)}', flush=True)
        last_action = action
        last_action_time = now

    if not clients:
        print(f'HandTracker: gesture {action} dropped because no client is connected', flush=True)

    msg = json.dumps({"type": "gesture", "action": action}) + "\n"
    data = msg.encode('utf-8')
    dead = []
    with clients_lock:
        for c in list(clients):
            try:
                c.sendall(data)
            except Exception:
                dead.append(c)
        for d in dead:
            try:
                clients.remove(d)
                d.close()
            except:
                pass

while True:
    success, img = capture.read()
    if not success:
        time.sleep(0.01)
        continue

    img = detector.findHands(img)
    lmList = detector.findPosition(img)
    action = None
    if len(lmList) != 0:
        try:
            action = detector.detect_fingers(img)
        except Exception:
            action = None

    if action:
        # Map detector actions to Godot input names
        if action == 'top_left':
            game_action = 'move_left_up'
        elif action == 'top_right':
            game_action = 'move_right_up'
        elif action == 'left':
            game_action = 'move_left'
        elif action == 'right':
            game_action = 'move_right'
        elif action == 'up':
            game_action = 'jump'
        elif action == 'left_up':
            game_action = 'move_left_up'
        elif action == 'right_up':
            game_action = 'move_right_up'
        else:
            game_action = None

        if game_action:
            send_gesture(game_action)

    cTime = time.time()
    fps = 1 / (cTime - pTime) if (cTime - pTime) > 0 else 0
    pTime = cTime

    cv2.putText(img, str(int(fps)), (10, 70), cv2.FONT_HERSHEY_PLAIN, 3, (255, 0, 255), 3)

    cv2.imshow("Image", img)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# Cleanup
for c in clients:
    try:
        c.close()
    except:
        pass
server.close()
capture.release()
cv2.destroyAllWindows()
try:
    os.remove(PID_FILE)
except Exception:
    pass