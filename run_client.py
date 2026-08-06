#!/usr/bin/env python3

import concurrent.futures
import os
import requests
import shlex
import shutil
import subprocess
import sys
import threading
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from argparse import ArgumentParser
from pathlib import Path
from pathlib import PurePosixPath

script_dir = Path(__file__).resolve().parent

CLIENT_CDN_URL = "https://opensourcefreerealms.com/client"
CLIENT_MANIFEST_URL = "https://opensourcefreerealms.com/clientmanifest.xml"
DOWNLOAD_CHUNK_SIZE = 1024 * 1024
DOWNLOAD_WORKERS = 16
DEFAULT_LAUNCH_ARGS = [
    "AssetDelivery:IndirectServerAddress=http://osfr.editz.dev/assets",
    "Portrait:UploadUrl=http://127.0.0.1:20040/image",
]

def user_data_dir(app_name):
    if sys.platform == "win32":
        base = os.environ.get("LOCALAPPDATA") or (Path.home() / "AppData" / "Local")
    elif sys.platform == "darwin":
        base = Path.home() / "Library" / "Application Support"
    else:
        base = os.environ.get("XDG_DATA_HOME") or (Path.home() / ".local" / "share")
    return Path(base) / app_name


def format_size(size):
    value = float(size)
    for unit in ("B", "KiB", "MiB", "GiB", "TiB"):
        if value < 1024 or unit == "TiB":
            return f"{value:.1f} {unit}"
        value /= 1024


def read_manifest():
    request = urllib.request.Request(CLIENT_MANIFEST_URL, headers={"User-Agent": "Sanctuary dev client setup"})
    with urllib.request.urlopen(request, timeout=30) as response:
        root = ET.parse(response).getroot()

    files = []

    def visit(element, parent=PurePosixPath()):
        for child in element:
            if child.tag == "Folder":
                name = child.get("name")
                folder = parent / name if name else parent
                visit(child, folder)
            elif child.tag == "File":
                path = parent / child.attrib["name"]
                if path.is_absolute() or ".." in path.parts:
                    raise ValueError(f"Unsafe path in client manifest: {path}")
                files.append((path, int(child.attrib["size"])))

    visit(root)
    return files


def download_client(client_folder):
    files = read_manifest()
    total_size = sum(size for _, size in files)
    total_files = len(files)
    staging_folder = client_folder.with_name(f".{client_folder.name}.download")

    if staging_folder.exists():
        shutil.rmtree(staging_folder)
    staging_folder.mkdir(parents=True)

    progress_lock = threading.Lock()
    downloaded = 0
    completed = 0

    def report():
        percent = downloaded * 100 / total_size if total_size else 100
        print(
            f"\rDownloading client: {percent:6.2f}% "
            f"({format_size(downloaded)} / {format_size(total_size)}) "
            f"[{completed}/{total_files}]",
            end="",
            flush=True,
        )

    def worker(relative_path, expected_size):
        nonlocal downloaded, completed
        destination = staging_folder.joinpath(*relative_path.parts)
        destination.parent.mkdir(parents=True, exist_ok=True)
        url = f"{CLIENT_CDN_URL}/{urllib.parse.quote(relative_path.as_posix())}"
        request = urllib.request.Request(url, headers={"User-Agent": "Sanctuary client setup"})

        file_size = 0
        with urllib.request.urlopen(request, timeout=30) as response, destination.open("wb") as output:
            content_type = response.headers.get_content_type()
            while chunk := response.read(DOWNLOAD_CHUNK_SIZE):
                output.write(chunk)
                file_size += len(chunk)
                with progress_lock:
                    downloaded += len(chunk)
                    report()

        # Cloudflare appends its analytics beacon to text/html responses, so tolerate the extra bytes.
        edge_injected_html = content_type == "text/html" and file_size >= expected_size
        if file_size != expected_size and not edge_injected_html:
            raise OSError(
                f"Size mismatch for {relative_path}: expected {expected_size} bytes, received {file_size}"
            )

        with progress_lock:
            completed += 1
            report()

    try:
        with concurrent.futures.ThreadPoolExecutor(max_workers=DOWNLOAD_WORKERS) as executor:
            futures = [executor.submit(worker, path, size) for path, size in files]
            for future in concurrent.futures.as_completed(futures):
                future.result()

        print()
        os.replace(staging_folder, client_folder)
        return client_folder
    except BaseException:
        print(file=sys.stderr)
        shutil.rmtree(staging_folder, ignore_errors=True)
        raise

def ensure_client():
    client_folder = script_dir / "client"
    if client_folder.exists() and client_folder.is_dir() and any(client_folder.iterdir()):
        # assume client already set up
        return client_folder

    if client_folder.exists():
        if client_folder.is_file():
            client_folder.unlink()
        else:
            client_folder.rmdir() # guaranteed to be empty
    
    # First, try to copy the public server client from the launcher
    # (AppData/Local/OSFRLauncher/Servers/OSFR Public Server/Client)
    launcher_app_data_path = user_data_dir("OSFRLauncher")
    public_server_client_path = launcher_app_data_path / "Servers" / "OSFR Public Server" / "Client"

    if public_server_client_path.exists() and public_server_client_path.is_dir() and any(public_server_client_path.iterdir()):
        shutil.copytree(public_server_client_path, client_folder)
        print(f"Copied client installation from launcher: {public_server_client_path}")
        return client_folder
    
    # Download the client from the public server's CDN.
    return download_client(client_folder)

def configure_client(client_folder):
    # To allow for multiple instances, all writable asset packs (AssetsW_XXX.pack)
    # need to be moved to the reado-only asset pack set (Assets_XXX.pack)
    last_read_only_asset_pack_number = 0
    for asset_pack in sorted(client_folder.glob("Assets_*.pack")):
        last_read_only_asset_pack_number = int(asset_pack.stem.split("_")[1])
    
    for writable_asset_pack in sorted(client_folder.glob("AssetsW_*.pack")):
        read_only_asset_pack_number = last_read_only_asset_pack_number + 1
        read_only_asset_pack = client_folder / f"Assets_{read_only_asset_pack_number:03}.pack"
        writable_asset_pack.rename(read_only_asset_pack)
        print(f"{writable_asset_pack} -> {read_only_asset_pack}")
        last_read_only_asset_pack_number = read_only_asset_pack_number

def register_account(address, port, username, password):
    api_server_url = f"http://{address}:{port}"
    register_endpoint = f"{api_server_url}/register"
    response = requests.post(register_endpoint, json={"username": username, "password": password})
    response.raise_for_status()

def get_session(address, port, username, password):
    api_server_url = f"http://{address}:{port}"
    login_endpoint = f"{api_server_url}/login"
    response = requests.post(login_endpoint, json={"username": username, "password": password})
    
    if response.status_code != 200:
        print(f"Failed to login ({response.status_code}); trying to register account...")
        register_account(address, port, username, password)
        print(f"Registered account; retrying login...")
        return get_session(address, port, username, password)
    
    return response.json()

def run_client(client_folder, address, port, args):
    exe_path = client_folder / "FreeRealms.exe"
    if not exe_path.exists():
        raise FileNotFoundError(f"Client executable not found: {exe_path}")
    
    full_args = []

    if sys.platform != "win32":
        full_args.append("wine")

    full_args.append(str(exe_path))
    full_args.append("Internationalization:Locale=8")
    full_args.append(f"Server={address}:{port}")
    full_args.extend(args)

    print(f"Running client with arguments: {' '.join(full_args)}")
    subprocess.run(full_args, cwd=str(client_folder))

def run_client_for_gateway(client_folder, address, port, character_id):
    print("Running in gateway mode. The gateway server must be running.")
    print()

    if port is None:
        port = 20260

    character_guid = (int(character_id) << 4) + 1

    ticket = uuid.uuid4()
    ticket = str(ticket).replace("-", "")

    args = []
    args.append(f"GUID={character_guid}")
    args.append(f"Ticket={ticket}")
    args.extend(DEFAULT_LAUNCH_ARGS)
    run_client(client_folder, address, port, args)

def run_client_for_login(client_folder, address, port, username, password):
    print("Running in login mode. The WebAPI and Login servers must be running.")
    print("After you create a character, you can connect to the gateway server directly in gateway mode:")
    print("  python run_client.py -g 1")
    print()

    api_server_port = 5000
    try:
        session = get_session(address, api_server_port, username, password)
    except requests.RequestException as e:
        print(f"Failed to get session: {e}")
        sys.exit(1)

    print(session)

    session_id = session["sessionId"]
    launch_args = session["launchArguments"]

    if port is None:
        port = 20042

    args = []
    args.append(f"SessionId={session_id}")
    
    if launch_args is None:
        args.extend(DEFAULT_LAUNCH_ARGS)
    else:
        args.extend(shlex.split(launch_args))

    run_client(client_folder, address, port, args)

def parse_args():
    parser = ArgumentParser(
        prog="run_client",
        description="Prepare, run, & connect a Free Realms client to a Sanctuary server."
    )

    parser.add_argument("-a", "--address",
                        help="Address of the server to connect to",
                        default="localhost")
    
    parser.add_argument("-g", "--gateway",
                        metavar="GATEWAY_CHARACTER_ID",
                        help="Connect to the gateway server with this character ID (character must already exist)")
    
    parser.add_argument("-l", "--login",
                        metavar="LOGIN_USERNAME",
                        help="Connect to the login server with this account (account must already exist)")
    
    args = parser.parse_args()

    # By default, connect to the login server with the test account
    if args.gateway is None and args.login is None:
        args.login = "test"

    return args

if __name__ == "__main__":
    args = parse_args()

    client_folder = ensure_client()
    if client_folder is None:
        print("Failed to find a game client to use.")
        sys.exit(1)

    configure_client(client_folder)

    address = args.address
    port = None
    address_parts = args.address.split(":")
    if len(address_parts) == 2:
        address = address_parts[0]
        port = address_parts[1]

    if args.gateway is not None:
        run_client_for_gateway(client_folder, address, port, args.gateway)
    elif args.login is not None:
        run_client_for_login(client_folder, address, port, args.login, "testtest")
    else:
        raise ValueError("Either a gateway character ID or a login username must be specified.")
