docker rm /Mentalist.ReverseProxy -f

docker build --no-cache --force-rm -t Mentalist.ReverseProxy -f Dockerfile .
docker run -d -p 40000:40000 --name Mentalist.ReverseProxy Mentalist.ReverseProxy

docker image prune --filter label=stage=build -f