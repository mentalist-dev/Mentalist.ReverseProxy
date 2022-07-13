set version=1.3.13

docker build --no-cache --force-rm -t reverse-proxy:%version% --build-arg VERSION=%version% -f Dockerfile .
docker tag reverse-proxy:%version% reverse-proxy:latest

docker tag reverse-proxy:%version% mentalistdev/reverse-proxy:%version%
docker push mentalistdev/reverse-proxy:%version%

docker tag reverse-proxy:%version% mentalistdev/reverse-proxy:latest
docker push mentalistdev/reverse-proxy:latest

docker image prune --filter label=stage=build -f
