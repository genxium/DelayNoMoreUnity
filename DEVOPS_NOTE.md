# Freeing buffer/cache memory
It's observed that when available memory is low but buffer/cache memory is high in `top`, backend performance dropped significantly, resulting in high delay and frequent graphically inconsistent rollbacks. Free the buffer/cache memory by
```
# free && sync && echo 3 > /proc/sys/vm/drop_caches && free
``` 

Reference https://unix.stackexchange.com/questions/87908/how-do-you-empty-the-buffers-and-cache-on-a-linux-system.
