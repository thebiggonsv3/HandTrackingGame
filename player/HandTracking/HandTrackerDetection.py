import cv2
import mediapipe as mp
import numpy as np

class HandDetector:
    def __init__(self, mode=False, maxHands=2, modelComp=1, detectionCon=0.5, trackCon=0.5):
        self.mode = mode
        self.maxHands = maxHands
        self.detectionCon = detectionCon
        self.trackCon = trackCon
        self.modelComp = modelComp

        self.mpHands = mp.solutions.hands
        self.hands = self.mpHands.Hands(self.mode, self.maxHands, self.modelComp, self.detectionCon, self.trackCon)
        self.mpDraw = mp.solutions.drawing_utils

    def findHands(self, img, draw=True):
        imgRGB = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        self.results = self.hands.process(imgRGB)
            
        if self.results.multi_hand_landmarks:
            for handLandmarks in self.results.multi_hand_landmarks:
                if draw:
                    self.mpDraw.draw_landmarks(img, handLandmarks, self.mpHands.HAND_CONNECTIONS)
        return img

    def findPosition(self, img, handNo=0, draw=True):
        lmList = []
        if self.results.multi_hand_landmarks:
            myHand =self.results.multi_hand_landmarks[handNo]
            for id, landmark in enumerate(myHand.landmark):
                height, width, channel = img.shape
                cx, cy = int(landmark.x * width), int(landmark.y * height)
                lmList.append([id, cx, cy])
                if draw:
                    cv2.circle(img, (cx, cy), 5, (255, 0, 255), cv2.FILLED)
        return lmList

    def detect_fingers(self, frame):
        if not self.results.multi_hand_landmarks:
            return None
        lm = self.results.multi_hand_landmarks[0].landmark

        def finger_angle(tip_id, pip_id, mcp_id):
            tip = np.array([lm[tip_id].x, lm[tip_id].y, lm[tip_id].z])
            pip = np.array([lm[pip_id].x, lm[pip_id].y, lm[pip_id].z])
            mcp = np.array([lm[mcp_id].x, lm[mcp_id].y, lm[mcp_id].z])
            v1, v2 = tip - pip, mcp - pip
            cos_a = np.dot(v1, v2) / (np.linalg.norm(v1) * np.linalg.norm(v2) + 1e-6)
            return np.degrees(np.arccos(np.clip(cos_a, -1, 1)))

        def finger_curled(tip_id, mid_id, mcp_id):
            tip  = np.array([lm[tip_id].x, lm[tip_id].y, lm[tip_id].z])
            mid  = np.array([lm[mid_id].x, lm[mid_id].y, lm[mid_id].z])
            mcp  = np.array([lm[mcp_id].x, lm[mcp_id].y, lm[mcp_id].z])
            palm = np.array([lm[0].x,      lm[0].y,       lm[0].z])
            tip_closer = np.linalg.norm(tip - palm) < np.linalg.norm(mcp - palm)
            mid_closer = np.linalg.norm(mid - palm) < np.linalg.norm(mcp - palm) * 1.2
            return tip_closer and mid_closer

        THRESHOLD = 165

        index_up  = finger_angle(8,  7, 5)  > THRESHOLD
        middle_up = finger_angle(12, 11, 9) > THRESHOLD
        ring_up   = finger_angle(16, 15, 13) > THRESHOLD
        pinky_up  = finger_angle(20, 19, 17) > THRESHOLD

        is_fist = all(finger_curled(tip, mid, mcp) for tip, mid, mcp in [
            (8,  7, 5),
            (12, 11, 9),
            (16, 15, 13),
            (20, 19, 17),
        ])

        is_pointer = (index_up and not middle_up and not ring_up and not pinky_up)

        if is_fist:
            cv2.putText(frame, "Fist", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0,0,255), 2, cv2.LINE_AA)
            return "attack"

        if is_pointer:
            mcp = np.array([lm[5].x, lm[5].y])
            tip = np.array([lm[8].x, lm[8].y])
            dx = tip[0] - mcp[0]
            dy = tip[1] - mcp[1]
            angle = np.degrees(np.arctan2(-dy, dx))

            if -45 <= angle <= 45:
                cv2.putText(frame, "Pointing Right", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0,255,0), 2, cv2.LINE_AA)
                return "right"
            elif 45 < angle <= 70:
                cv2.putText(frame, "Pointing Top-Right", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0,220,100), 2, cv2.LINE_AA)
                return "top_right"
            elif 70 < angle <= 110:
                cv2.putText(frame, "Pointing Up", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0,255,180), 2, cv2.LINE_AA)
                return "up"
            elif 110 < angle <= 135:
                cv2.putText(frame, "Pointing Top-Left", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0,180,220), 2, cv2.LINE_AA)
                return "top_left"
            else:
                cv2.putText(frame, "Pointing Left", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0,140,255), 2, cv2.LINE_AA)
                return "left"

        return None