FROM python:3.12-slim-bookworm

LABEL org.opencontainers.image.title="Microsoft Foundry Hosted Agents Python Responses Base" \
      org.opencontainers.image.description="Model-backed Python agent for Microsoft Foundry Hosted Agents using the Responses protocol" \
      org.opencontainers.image.version="1.1.0"

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

COPY requirements.txt .
RUN python -m pip install --upgrade pip \
    && python -m pip install -r requirements.txt \
    && useradd --create-home --uid 10001 --shell /usr/sbin/nologin app

COPY --chown=app:app main.py .

USER app

EXPOSE 8088

CMD ["python", "/app/main.py"]
