// Linux test double for the Windows API contract, not a Windows integration test.
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
static int outstanding,mode;
static const uint16_t alpha[]={ 'a','l','p','h','a',0 }, beta[]={'b','e','t','a',0},gone[]={'g','o','n','e',0};
static void* allocate(size_t n){outstanding++;return calloc(1,n);}
int TestOutstandingBuffers(void){return outstanding;}
void TestMode(int m){mode=m;}
int NetApiBufferFree(void* p){if(p){outstanding--;free(p);}return 0;}
int NetUserEnum(const uint16_t* server,uint32_t level,uint32_t filter,void** buf,uint32_t max,uint32_t* count,uint32_t* total,uint32_t* resume){
 (void)server;(void)max;*buf=NULL;*count=0;*total=3;
 if(level!=0)return 124;
 if(filter!=2)return 87;
 if(mode==1){*buf=allocate(8);return 5;}
 if(mode==3)return 0;
 if(*resume==0){const uint16_t** p=allocate(2*sizeof(void*));p[0]=alpha;p[1]=beta;*buf=p;*count=2;*resume=1;return 234;}
 const uint16_t** p=allocate(sizeof(void*));p[0]=gone;*buf=p;*count=1;*resume=0;return 0;
}
struct user23{const uint16_t *name,*full,*comment;uint32_t flags;void* sid;};
int NetUserGetInfo(const uint16_t* server,const uint16_t* name,uint32_t level,void** buf){
 (void)server;*buf=NULL;if(level!=23)return 124;
 if(name[0]=='g')return 2221;
 if(mode==2){*buf=allocate(8);return 5;}
 struct user23* p=allocate(sizeof(struct user23)+16);
 uint8_t* sid=(uint8_t*)(p+1);sid[0]=1;sid[1]=2;sid[7]=5;
 uint32_t sub=21;memcpy(sid+8,&sub,4);sub=name[0]=='a'?1001:1002;memcpy(sid+12,&sub,4);
 p->name=name[0]=='a'?alpha:beta;p->flags=name[0]=='a'?0x200:0x202;p->sid=sid;*buf=p;return 0;
}
