// 服务端 RSA 公钥 (SPKI / DER, base64)。
// 用于登录时对邮箱、密码以及刷新令牌做 RSA-OAEP(SHA-1) 加密，
// 与后端 PassportService.Decrypt 使用的 RSAEncryptionPadding.OaepSHA1 对应。
//
// 该值由服务端私钥 (server/privkey.key) 推导得到，属于公钥，可安全放在前端。
// 若后端更换了 RSA 私钥，请同步更新此处：
//   openssl rsa -in privkey.key -pubout -outform DER | base64 -w0
export const RSA_PUBLIC_KEY_DER_BASE64 =
  'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Uv7IhpG+gBdwz8PFs0JGxblkYV+KIc8Yv1iNpXmT8mxLfNIStsn3ypSWlsdfikVU1CoPNGQUQWy6RAq1/SDOtHxUR1b/2E4HgPiGSwQRNnvdmi9FcgQx0pc6baNuHaF1HDSQRPh4a/LrJ2qGnMws0CZFNsFvXD0FbmJHfxJTZc+V+CZW28OLLeD576HuJO2LI6pHWj7yFjTxYKj2jfYq9PdjsJmOtMZwmazYhrWuiuoXriFmAMQ+MxcqRAUZGHbkMh3qUJJJi6baB99/xFKaStB7P/8FFy8P1zbKGNanZtZER6Ji7YmIikllS6hi4WUcXu0314ZDFu4K2ephJzudwIDAQAB'
