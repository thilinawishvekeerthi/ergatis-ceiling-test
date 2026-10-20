from fastapi import FastAPI

APP_NAME = "ergatis-ceiling-test"
VERSION = "0.1.0"

app = FastAPI(title=APP_NAME, version=VERSION)


@app.get("/")
def root() -> dict[str, str]:
    return {"name": APP_NAME, "docs": "/docs"}


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/version")
def version() -> dict[str, str]:
    return {"version": VERSION}
