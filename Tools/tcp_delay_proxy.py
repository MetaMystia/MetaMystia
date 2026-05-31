import asyncio
import random

LISTEN_HOST = "127.0.0.1"
LISTEN_PORT = 40816
TARGET_HOST = "127.0.0.1"
TARGET_PORT = 40815
MIN_DELAY = 2
MAX_DELAY = 3


async def pipe(reader, writer, tag):
    try:
        while data := await reader.read(65536):
            await asyncio.sleep(random.uniform(MIN_DELAY, MAX_DELAY))
            writer.write(data)
            await writer.drain()
    except Exception as e:
        print(f"[{tag}] {e}")
    finally:
        writer.close()
        await writer.wait_closed()


async def handle(client_reader, client_writer):
    peer = client_writer.get_extra_info("peername")
    print(f"client {peer} connected")
    try:
        server_reader, server_writer = await asyncio.open_connection(TARGET_HOST, TARGET_PORT)
    except Exception as e:
        print(f"connect target failed: {e}")
        client_writer.close()
        await client_writer.wait_closed()
        return

    await asyncio.gather(
        pipe(client_reader, server_writer, "client->server"),
        pipe(server_reader, client_writer, "server->client"),
    )
    print(f"client {peer} closed")


async def main():
    server = await asyncio.start_server(handle, LISTEN_HOST, LISTEN_PORT)
    print(f"listening on {LISTEN_HOST}:{LISTEN_PORT}, forwarding to {TARGET_HOST}:{TARGET_PORT}, delay {MIN_DELAY}-{MAX_DELAY}s")
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    asyncio.run(main())
