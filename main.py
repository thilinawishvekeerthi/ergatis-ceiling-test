from fastapi import FastAPI

app = FastAPI()


@app.get("/version")
def version():
    return {"version": "0.2.0"}
