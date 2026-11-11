// 服务端 RSA 公钥 (SPKI / DER, base64)，公钥可安全放在前端.
// 登录后端更换私钥时用下面这条命令重新导出：
//   openssl rsa -in privkey.key -pubout -outform DER | base64 -w0
export const RSA_PUBLIC_KEY_DER_BASE64 =
  'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Uv7IhpG+gBdwz8PFs0JGxblkYV+KIc8Yv1iNpXmT8mxLfNIStsn3ypSWlsdfikVU1CoPNGQUQWy6RAq1/SDOtHxUR1b/2E4HgPiGSwQRNnvdmi9FcgQx0pc6baNuHaF1HDSQRPh4a/LrJ2qGnMws0CZFNsFvXD0FbmJHfxJTZc+V+CZW28OLLeD576HuJO2LI6pHWj7yFjTxYKj2jfYq9PdjsJmOtMZwmazYhrWuiuoXriFmAMQ+MxcqRAUZGHbkMh3qUJJJi6baB99/xFKaStB7P/8FFy8P1zbKGNanZtZER6Ji7YmIikllS6hi4WUcXu0314ZDFu4K2ephJzudwIDAQAB';
