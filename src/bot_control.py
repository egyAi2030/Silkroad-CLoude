import requests
import sys
import json
import time
import random

BASE_URL = "http://localhost:6005"

def usage():
    print("SilkroadAIBot CLI Controller")
    print("Commands:")
    print("  launch           - Start sro_client.exe with proxy")
    print("  passive          - PASSIVE MODE: Launch -> You Login -> Bot Detects")
    print("  login            - Authenticate and enter world using stored ID/PW")
    print("  status           - Get bot connection status")
    print("  state            - Get character position and HP/MP")
    print("  logs             - Get recent action success/fail logs")
    print("  random           - AUTOMATED: Launch -> Login -> Random Actions")
    print("  stress           - LIVE: Start random actions on current session")

def send_post(path, data=None):
    try:
        r = requests.post(f"{BASE_URL}/{path}", json=data, timeout=10)
        return r.json()
    except Exception as e:
        print(f"POST Error {path}: {e}")
        return None

def send_get(path):
    try:
        r = requests.get(f"{BASE_URL}/{path}", timeout=10)
        return r.json()
    except Exception as e:
        print(f"GET Error {path}: {e}")
        return None

def wait_for_state(field, expected_value, timeout=180):
    print(f"Waiting for {field} to be {expected_value} (Max {timeout}s)...")
    start_time = time.time()
    while time.time() - start_time < timeout:
        status = send_get("status")
        if status and status.get(field) == expected_value:
            print(f"Confirmed: {field} is {expected_value}")
            return True
        time.sleep(5)
    print(f"Timeout waiting for {field}")
    return False

def do_live_stress():
    print("Starting Live Stress Test... Character detected. CTRL+C to stop.")
    while True:
        status = send_get("status")
        if not status or not status.get("characterInWorld"):
            print("ERROR: Character not in world. Entry required first.")
            break

        state = send_get("state")
        if not state or not state.get("character"):
            time.sleep(2)
            continue
            
        actions = ["move", "attack", "useitem"]
        choice = random.choice(actions)
        
        if choice == "move":
            char_pos = state["character"]["position"]
            tx = char_pos["x"] + random.uniform(-10, 10)
            ty = char_pos["y"] + random.uniform(-10, 10)
            print(f"Action: Move to {tx:.1f}, {ty:.1f}")
            send_post("move", {"x": tx, "y": ty})
            
        elif choice == "attack":
            entities = state.get("entities", [])
            # In live world, Entity class name might be 'SRMob'
            mobs = [e for e in entities if "Mob" in e.get("type", "")]
            if mobs:
                target = random.choice(mobs)
                print(f"Action: Attack {target['name']} (UID:{target['uid']})")
                send_post("attack", {"uid": target["uid"]})
            else:
                print("Action: No mobs nearby.")
                
        elif choice == "useitem":
            print("Action: Use Item Slot 1")
            send_post("useitem", {"slot": 1})

        time.sleep(7)
        logs = send_get("logs")
        if logs and logs.get("logs"):
            print(f"SERVER VERIFICATION: {logs['logs'][-1]}")

def do_passive_run():
    # 1. Launch
    print("Step 1: Launching Client...")
    send_post("launch")
    print("Waiting for Client Engine...")
    if not wait_for_state("clientRunning", True):
        return

    print("Step 2: PLEASE LOGIN MANUALLY IN THE GAME CLIENT.")
    print("Bot is now monitoring proxy traffic for your character...")
    
    if not wait_for_state("characterInWorld", True, timeout=600):
        print("Timeout waiting for manual login.")
        return

    print("SUCCESS: Character detected in game world!")
    do_live_stress()

def do_automated_run():
    # 1. Launch
    print("Step 1: Launching Client...")
    send_post("launch")
    print("Waiting 30 seconds for Client Engine to initialize...")
    time.sleep(30)
    
    if not wait_for_state("clientRunning", True):
        return

    # 2. Login
    print("Step 2: Waiting 15 seconds for Gateway handshake stability...")
    time.sleep(15)
    
    print("Triggering Login/Authentication sequence...")
    send_post("login")
    
    print("Waiting 20 seconds for character list and selection...")
    time.sleep(20)
    
    # 3. Wait for World Entry
    if not wait_for_state("characterInWorld", True, timeout=240):
        print("Character failed to enter world. Check bot logs for error codes.")
        return

    print("Step 3: Character is in world!")
    do_live_stress()

if __name__ == "__main__":
    if len(sys.argv) < 2:
        usage()
        sys.exit(1)

    cmd = sys.argv[1].lower()
    
    if cmd == "launch":
        print(json.dumps(send_post("launch"), indent=2))
    elif cmd == "login":
        print(json.dumps(send_post("login"), indent=2))
    elif cmd == "status":
        status = send_get("status")
        if status:
            print("=== BOT STATUS ===")
            print(f"Server Connected:  {status.get('serverConnected')}")
            print(f"Client Linked:     {status.get('clientLinked')}")
            print(f"Client Running:    {status.get('clientRunning')}")
            print(f"Character World:   {status.get('characterInWorld')}")
            print(f"Character Name:    {status.get('characterName')}")
        else:
            print("Error: Could not get status.")
    elif cmd == "state":
        print(json.dumps(send_get("state"), indent=2))
    elif cmd == "logs":
        print(json.dumps(send_get("logs"), indent=2))
    elif cmd == "random":
        do_automated_run()
    elif cmd == "passive":
        do_passive_run()
    elif cmd == "stress":
        do_live_stress()
    else:
        usage()
