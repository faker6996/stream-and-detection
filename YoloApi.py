# -*- coding: utf-8 -*-
import requests
import random
import time

API_URL = "https://localhost:44359/api/BoundingBox"
CAMERA_IDS = ["01", "02", "03", "04","05", "06", "07", "08"]

IMAGE_WIDTH = 1920
IMAGE_HEIGHT = 1080
BOX_SIZE = 200

def create_random_box():
    max_x = IMAGE_WIDTH - BOX_SIZE
    max_y = IMAGE_HEIGHT - BOX_SIZE
    x = random.randint(0, max_x)
    y = random.randint(0, max_y)
    return {"X": x, "Y": y, "Width": BOX_SIZE, "Height": BOX_SIZE}

while True:
    for cam_id in CAMERA_IDS:
        payload = {
            "cameraId": cam_id,
            "boxes": [ { **create_random_box(), "Label": cam_id } ]
        }

        try:
            response = requests.post(API_URL, json=payload, timeout=5, verify=False)
            print(f"{cam_id} ✅ Status: {response.status_code}, Response: {response.text}")
        except requests.exceptions.RequestException as e:
            print(f"{cam_id} ❌ Error during request:", e)

    print("⏳ Waiting 20s before next round...\n")
    time.sleep(0.03)
