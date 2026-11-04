#!/usr/bin/env python3
"""
The test VM's one way in and out, for the length of one run: serves payload.zip to it and takes out.zip back, under a
random token, then exits once out.zip has arrived (exit 0) or the time runs out (exit 1).

    exchange.py --bind 192.168.20.30 --port 8765 --token <random> --payload payload.zip --out out.zip --timeout 1800

    GET  /<token>/payload.zip   the build and the test to run
    PUT  /<token>/out.zip       what the run left behind: its log, exit code, screenshots
"""
import argparse
import http.server
import os
import sys
import threading


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--bind", required=True)
    p.add_argument("--port", type=int, required=True)
    p.add_argument("--token", required=True)
    p.add_argument("--payload", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--timeout", type=int, default=1800)
    a = p.parse_args()

    done = threading.Event()

    class Handler(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path != f"/{a.token}/payload.zip":
                return self.send_error(404)
            with open(a.payload, "rb") as f:
                body = f.read()
            self.send_response(200)
            self.send_header("Content-Type", "application/zip")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_PUT(self):
            if self.path != f"/{a.token}/out.zip":
                return self.send_error(404)
            length = int(self.headers.get("Content-Length", "0"))
            tmp = a.out + ".part"
            with open(tmp, "wb") as f:
                remaining = length
                while remaining:
                    chunk = self.rfile.read(min(remaining, 1 << 20))
                    if not chunk:
                        break
                    f.write(chunk)
                    remaining -= len(chunk)
            if remaining:
                os.remove(tmp)
                return self.send_error(400, "short body")
            os.replace(tmp, a.out)
            self.send_response(204)
            self.end_headers()
            done.set()

        def log_message(self, fmt, *args):
            sys.stderr.write("[exchange] %s %s\n" % (self.address_string(), fmt % args))

    server = http.server.ThreadingHTTPServer((a.bind, a.port), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    ok = done.wait(a.timeout)
    server.shutdown()
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
