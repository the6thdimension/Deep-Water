import os

collection = os.getcwd()


for i, filename in enumerate(os.listdir(collection)):
    if ("png" in filename):

        print(filename[:-4])
        os.rename(filename, filename[:-4] + ".png")
        # print(collection + filename)
        # os.rename(collection + filename, collection + )
    # os.rename()
#     os.rename("C:/darth_vader/" + filename, "C:/darth_vader/" + str(i) + ".jpg")